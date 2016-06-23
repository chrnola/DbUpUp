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

    // TODO: There has to be a more idiotmatic way of doing this...
    if Seq.isEmpty dupes then
        None
    else
        Some(dupes)

let getEffectiveOrder unionedScripts = 
    let { rootScripts = parsedRootScripts; extScripts = parsedExtScripts } = unionedScripts

    let groupedByParent = 
        parsedExtScripts
            |> Seq.groupBy (fun extScript -> extScript.ParentId.Value)
            |> dict
    
    parsedRootScripts
     |> Seq.collect (fun rootScript -> rootScript :: parsedExtScripts |> Seq.where (fun extScript -> extScript.ParentId.Value = rootScript.Id))

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

    match unionedScripts |> findDuplicateIds with
        | Some dupes -> reportDupes dupes
        | None -> printfn "No dupes detected"

    unionedScripts
     |> getEffectiveOrder
     |> Seq.iter (fun script -> printfn "Execute script: %s" script.Path)

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
