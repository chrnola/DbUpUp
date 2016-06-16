open FSharp.Data
open System.IO

type manifest = JsonProvider<"manifest.json">

let pathToRoot = "..\..\..\SampleWorkspace\Root";

let findManifestFileInDir (path:string) =
    let fullPath = Path.Combine [|path; "manifest.json"|]

    match File.Exists fullPath with
        | true -> Some(fullPath)
        | false -> None

let parseManifestFile pathToManifest =
    match pathToManifest with
        | Some path -> File.OpenText path |> manifest.Load
        | None -> failwithf "This directory doesn't contain a manifest file!"

let findDuplicateIds (scripts:manifest.Script[]) =
    let dupes =
        scripts
            |> Array.toList
            |> List.groupBy (fun script -> script.Id)
            |> List.where (fun (key, values) -> List.length values > 1)
            |> List.map (fun (key, values) -> (key, List.length values))

    // TODO: There has to be a more idiotmatic way of doing this...
    if List.isEmpty dupes then
        None
    else
        Some(dupes)

let reportDupes (dupes:(System.Guid * int) list) =
    dupes |> List.iter (fun (id, count) -> printfn "ID: %A appears %i times" id count)
    failwithf "Manifest contained duplicated IDs!"

[<EntryPoint>]
let main argv = 
    let parsedManifestScripts = (findManifestFileInDir pathToRoot |> parseManifestFile).Scripts

    printfn "%A" parsedManifestScripts

    match parsedManifestScripts |> findDuplicateIds with
        | Some dupes -> reportDupes dupes
        | None -> printfn "No dupes detected"

    printfn "Done"
    System.Console.ReadLine() |> ignore
    0 // return an integer exit code
