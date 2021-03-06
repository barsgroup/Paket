/// Getting help docs from Paket.exe
#r "../../bin/Argu.dll"
#r "../../bin/Paket.exe"
open System.IO


#if COMMANDS
Paket.Commands.getAllCommands()
|> List.iter (fun command ->
    let metadata = command.ParentInfo |> Option.get
    let additionalText = 
        let verboseOption = """

If you add the `-v` flag, then Paket will run in verbose mode and show detailed information.

With `--log-file [FileName]` you can trace the logged information into a file.

"""
        let optFile = sprintf "../content/commands/%s.md" metadata.Name
        if File.Exists optFile
        then verboseOption + File.ReadAllText optFile
        else verboseOption
    // Work around bug tpetricek/FSharp.Formatting#321 (FSharp.Literate does not escape HTML entities in code blocks with unknown language)
    let cleanText (text : string) = text.Replace("[lang=batchfile]", "[lang=msh]").Replace("```batchfile", "```msh")
    File.WriteAllText(sprintf "../content/paket-%s.md" metadata.Name, Paket.Commands.markdown command additionalText |> cleanText))
#endif


// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "Paket.Core.dll" ]

let githubLink = "http://github.com/fsprojects/Paket"

// Specify more information about your project
let info =
  [ "project-name", "Paket"
    "project-author", "Steffen Forkmann, Alexander Gross"
    "project-summary", "A dependency manager for .NET with support for NuGet packages and git repositories."
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/Paket" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#load "../../packages/build/FSharp.Formatting/FSharp.Formatting.fsx"
#I "../../packages/build/FAKE/tools/"
#r "NuGet.Core.dll"
#r "FakeLib.dll"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "../../bin"
let content    = __SOURCE_DIRECTORY__ @@ "../content"
let output     = __SOURCE_DIRECTORY__ @@ "../output"
let files      = __SOURCE_DIRECTORY__ @@ "../files"
let templates  = __SOURCE_DIRECTORY__ @@ "templates"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/build/FSharp.Formatting/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

// Where to look for *.csproj templates (in this order)
let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[ templates; formatting @@ "templates"
                          formatting @@ "templates/reference" ])
subDirectories (directoryInfo templates)
|> Seq.iter (fun d ->
                let name = d.Name
                if name.Length = 2 || name.Length = 3 then
                    layoutRootsAll.Add(
                            name, [templates @@ name
                                   formatting @@ "templates"
                                   formatting @@ "templates/reference" ]))

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  CopyRecursive (formatting @@ "styles") (output @@ "content") true 
    |> Log "Copying styles and scripts: "

// Build API reference from XML comments
let buildReference () =
  CleanDir (output @@ "reference")
  let binaries =
    referenceBinaries
    |> List.map (fun lib-> bin @@ lib)
  MetadataFormat.Generate
    ( binaries, output @@ "reference", layoutRootsAll.["en"],
      parameters = ("root", "../")::info,
      sourceRepo = githubLink @@ "tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "..",
      publicOnly = true, libDirs = [bin] )

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories) 
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    let langSpecificPath(lang, path:string) =
        path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.exists(fun i -> i = lang)
    let layoutRoots =
        let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
        match key with
        | Some lang -> layoutRootsAll.[lang]
        | None -> layoutRootsAll.["en"] // "en" is the default language
    Literate.ProcessDirectory
      ( dir, docTemplate, output @@ sub, replacements = ("root", ".")::info,
        layoutRoots = layoutRoots,
        generateAnchors = true )

// Generate
copyFiles()
#if HELP
buildDocumentation()
#endif
#if REFERENCE
buildReference()
#endif
