#if INTERACTIVE
#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#endif

open FSharp.Data
open System.IO

type manifest = JsonProvider<"manifest.json">
type UnionManifests = { rootScripts: manifest.Script list; extScripts: manifest.Script list }

#if INTERACTIVE
// Sending to FSI in VS Code uses the root of the opened project Directory as the
// working directory.
let pathToRoot = "SampleWorkspace/Root";
let pathToExt = "SampleWorkspace/Extension";
#else
// Differs from the compiled case: sln\proj\bin\config
let pathToRoot = "..\..\..\SampleWorkspace\Root";
let pathToExt = "..\..\..\SampleWorkspace\Extension";
#endif

type Failures =
    | DuplicateIds of (System.Guid * int) seq

type Successes =
    | ParsedFiles of UnionManifests
    | FlattenedTree of manifest.Script seq

type ParseStatus =
    | Success of Successes
    | Failure of Failures

let reportResults = function
    | Success s ->
        match s with
        | ParsedFiles _ -> printfn "This is an intermediate step. Should probably be modeled differently..."
        | FlattenedTree scripts -> scripts |> Seq.iter (fun script -> printfn "Execute script: %s" script.Path)
    | Failure f -> 
        match f with
        | DuplicateIds dupes -> dupes |> Seq.iter (fun (id, count) -> printfn "ID: %A appears %i times" id count)

let bind f = function
    | Success s ->
        match s with
        | ParsedFiles s -> f s
    | Failure f -> Failure f

let findManifestFileInDir (path:string) =
    let fullPath = Path.Combine [|path; "manifest.json"|]

    match File.Exists fullPath with
        | true -> Some(fullPath)
        | false -> None

let parseManifestFile pathToManifest =
    match pathToManifest with
        | Some path -> (File.OpenText path |> manifest.Load).Scripts
        | None -> failwithf "This directory doesn't contain a manifest file!"

let findDuplicateIds unionedScripts =
    let { rootScripts = parsedRootScripts; extScripts = parsedExtScripts } = unionedScripts

    let dupes =
        parsedRootScripts @ parsedExtScripts
         // List funcs not defined in Mono on OS X?
         |> Seq.groupBy (fun script -> script.Id)
         |> Seq.map (fun (key, occurances) -> (key, Seq.length occurances))
         |> Seq.where (fun (_, occurances) -> occurances > 1)

    if Seq.isEmpty dupes then
        Success(ParsedFiles(unionedScripts))
    else
        Failure(DuplicateIds(dupes))

let getEffectiveOrder unionedScripts = 
    let { rootScripts = parsedRootScripts; extScripts = parsedExtScripts } = unionedScripts

    let groupedByParent = 
        parsedExtScripts
            |> Seq.groupBy (fun extScript -> extScript.ParentId.Value)
            |> dict
    
    let isScriptChildOfParent (parent:manifest.Script) (script:manifest.Script) =
        match script.ParentId with 
            | Some parentGuid -> parentGuid = parent.Id 
            | None -> false

    let getExtScriptsForRoot (rootScript:manifest.Script) =
        // Partially apply the rootScript to get a customized compare function
        let compareWithRoot = isScriptChildOfParent rootScript
        parsedExtScripts |> List.where compareWithRoot

    let flat = parsedRootScripts |> Seq.collect (fun rootScript -> rootScript :: getExtScriptsForRoot rootScript)
    FlattenedTree(flat)

let reportDupes (dupes:(System.Guid * int) seq) =
    dupes |> Seq.iter (fun (id, count) -> printfn "ID: %A appears %i times" id count)
    failwithf "Manifest contained duplicated IDs!"

let getScripts = findManifestFileInDir >> parseManifestFile

let parseRootAndExt rootPath extPath =
    let parsedRootScripts = getScripts rootPath |> List.ofArray
    let parsedExtScripts = getScripts extPath |> List.ofArray

    {rootScripts = parsedRootScripts; extScripts = parsedExtScripts}

let main' rootPath extPath =
    let unionedScripts = parseRootAndExt rootPath extPath
    let {rootScripts = parsedRootScripts; extScripts = parsedExtScripts} = unionedScripts

    printfn "Root scripts:"
    printfn "%A" parsedRootScripts
    printfn "Extension scripts:"
    printfn "%A" parsedExtScripts

    let processManifests = findDuplicateIds >> bind getEffectiveOrder >> reportResults

    unionedScripts |> processManifests

    printfn "Done"

#if INTERACTIVE
main' pathToRoot pathToExt
#else
[<EntryPoint>]
let main argv = 
    main' pathToRoot pathToExt
    System.Console.ReadLine() |> ignore
    0 // return an integer exit code
#endif
