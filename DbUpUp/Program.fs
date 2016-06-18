open FSharp.Data
open System.IO

type manifest = JsonProvider<"manifest.json">
type UnionManifests = { rootScripts: manifest.Script list; extScripts: manifest.Script list }

let pathToRoot = "..\..\..\SampleWorkspace\Root";
let pathToExt = "..\..\..\SampleWorkspace\Extension";

let findManifestFileInDir (path:string) =
    let fullPath = Path.Combine [|path; "manifest.json"|]

    match File.Exists fullPath with
        | true -> Some(fullPath)
        | false -> None

let parseManifestFile pathToManifest =
    match pathToManifest with
        | Some path -> File.OpenText path |> manifest.Load
        | None -> failwithf "This directory doesn't contain a manifest file!"

let findDuplicateIds unionedScripts =
    let { rootScripts = parsedRootScripts; extScripts = parsedExtScripts } = unionedScripts

    let dupes =
        parsedRootScripts @ parsedExtScripts
         |> List.groupBy (fun script -> script.Id)
         |> List.map (fun (key, occurances) -> (key, List.length occurances))
         |> List.where (fun (_, occurances) -> occurances > 1)

    // TODO: There has to be a more idiotmatic way of doing this...
    if List.isEmpty dupes then
        None
    else
        Some(dupes)

let getEffectiveOrder unionedScripts = 
    let { rootScripts = parsedRootScripts; extScripts = parsedExtScripts } = unionedScripts

    let groupedByParent = 
        parsedExtScripts
            |> List.groupBy (fun script -> script.ParentId.Value)
            |> dict



let reportDupes (dupes:(System.Guid * int) list) =
    dupes |> List.iter (fun (id, count) -> printfn "ID: %A appears %i times" id count)
    failwithf "Manifest contained duplicated IDs!"

let getScripts path = (findManifestFileInDir path |> parseManifestFile).Scripts

let parseRootAndExt rootPath extPath =
    let parsedRootScripts = getScripts rootPath |> List.ofArray
    let parsedExtScripts = getScripts extPath |> List.ofArray

    {rootScripts = parsedRootScripts; extScripts = parsedExtScripts}

[<EntryPoint>]
let main argv = 

    let unionedScripts = parseRootAndExt pathToRoot pathToExt
    let {rootScripts = parsedRootScripts; extScripts = parsedExtScripts} = unionedScripts

    printfn "Root scripts:"
    printfn "%A" parsedRootScripts
    printfn "Extension scripts:"
    printfn "%A" parsedExtScripts

    match unionedScripts |> findDuplicateIds with
        | Some dupes -> reportDupes dupes
        | None -> printfn "No dupes detected"

    printfn "Done"
    System.Console.ReadLine() |> ignore
    0 // return an integer exit code
