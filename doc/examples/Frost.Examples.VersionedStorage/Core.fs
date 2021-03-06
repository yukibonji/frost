﻿module Frost.Examples.VersionedStorage.Core

open System
open System.IO
open System.Text
open FSharpx
open LibGit2Sharp

// Repository

let private path = Repository.Init (@"C:\Repo", true)
let private repo = new Repository (path)

// Utils

let private head () =
    match repo.Commits |> Seq.isEmpty with
    | true -> None
    | _ -> Some repo.Head.Tip

let private commit sha =
    match sha with
    | None -> head ()
    | Some sha -> repo.Commits |> Seq.tryFind (fun x -> x.Sha = sha)

let private treeDefinition () =
    head ()
    |> Option.map (fun x -> TreeDefinition.From x.Tree)
    |> Option.getOrElse (TreeDefinition ())

let private parents () =
    head ()
    |> Option.map (fun x -> [ x ])
    |> Option.getOrElse []

let private sanitize (path: string) =
    path.Trim [| '/' |]

// Sha

let sha () =
    head ()
    |> Option.map (fun x -> x.Sha)
    |> Option.getOrElse "None"

// Create

let private addToTree path (data: Stream) =
    treeDefinition ()
    |> fun x -> x.Add (sanitize path, repo.ObjectDatabase.CreateBlob (data), Mode.NonExecutableFile)
    |> fun x -> repo.ObjectDatabase.CreateTree x

let create path data =
    let tree = addToTree path data
    let signature = Signature ("Andrew", "andrew@xyncro.com", DateTimeOffset.UtcNow)
    let commit = repo.ObjectDatabase.CreateCommit (signature, signature, "added", tree, parents (), false, Nullable ('x'))

    repo.Reset (ResetMode.Soft, commit)

// Delete

let private removeFromTree path =
    treeDefinition ()
    |> fun x -> x.Remove (sanitize path)
    |> fun x -> repo.ObjectDatabase.CreateTree x

let delete path =
    let tree = removeFromTree path
    let signature = Signature ("Andrew", "andrew@xyncro.com", DateTimeOffset.UtcNow)
    let commit = repo.ObjectDatabase.CreateCommit (signature, signature, "deleted", tree, parents (), false, Nullable ('x'))

    repo.Reset (ResetMode.Soft, commit)

// Read 

let read path sha =
    commit sha
    |> Option.bind (fun x -> 
        x.Tree.[sanitize path] |> function | null -> None | x -> Some x)
    |> Option.bind (fun x ->
        match x.Target with
        | :? Blob as x -> Some (x.GetContentStream ())
        | _ -> None)
    |> Option.map (fun x ->
        use ms = new MemoryStream ()
        x.CopyTo ms
        ms.ToArray ())
