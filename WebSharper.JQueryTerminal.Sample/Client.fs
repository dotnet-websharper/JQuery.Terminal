// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2026 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace WebSharper.JQueryTerminal.Sample

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.JQueryTerminal

[<JavaScript>]
module Client =

    type IndexTemplate = Template<"index.html", ClientLoad.FromDocument>

    // ---------------------------------------------------------------
    // View state
    // ---------------------------------------------------------------

    let status = Var.Create "Ready — the terminal is created from the npm jquery.terminal package."
    let output = Var.Create "No API call yet."
    let version = Var.Create "-"
    let geometry = Var.Create "-"
    let level = Var.Create "-"
    let settingsText = Var.Create "-"
    let historyText = Var.Create "-"
    let commandText = Var.Create ""
    let staticHelpers = Var.Create "-"

    [<Inline "new Date().toLocaleString()">]
    let private nowString () : string = X<string>

    let theme = Var.Create "light"
    let themeLabel = Var.Create "🌙 Dark mode"
    let themeKey = "ws-jqueryterminal-theme"

    let applyTheme () =
        JS.Document.DocumentElement.SetAttribute("data-theme", theme.Value)
        themeLabel.Value <-
            if theme.Value = "dark" then "☀️ Light mode" else "🌙 Dark mode"

    let toggleTheme () =
        theme.Value <- (if theme.Value = "dark" then "light" else "dark")
        try
            JS.Window.LocalStorage.SetItem(themeKey, theme.Value)
        with _ ->
            ()
        applyTheme ()

    // ---------------------------------------------------------------
    // Terminal state
    // ---------------------------------------------------------------

    let mutable terminal : option<Terminal> = None

    let withTerminal (f: Terminal -> unit) =
        match terminal with
        | Some t -> f t
        | None -> status.Value <- "The terminal is not available."

    let refreshState () =
        withTerminal (fun t ->
            let s = t.Settings()
            settingsText.Value <-
                sprintf "name=%s · history=%b · enabled=%b · historySize=%d"
                    (if isNull s.Name then "-" else s.Name)
                    s.History
                    s.Enabled
                    s.HistorySize
            let g = t.Geometry()
            geometry.Value <- sprintf "%d × %d" g.Rows g.Cols
            level.Value <- string (t.Level())
            historyText.Value <-
                let items = t.History().Data()
                if isNull items || items.Length = 0 then "(empty)"
                else items |> Array.truncate 6 |> String.concat " · "
        )

    let refreshStaticHelpers () =
        let parsed = Terminal.ParseCommand("echo one two")
        let args = Terminal.SplitArguments("one 'two three'") |> String.concat " | "
        let active = Terminal.Active()
        let history = Terminal.CreateHistory("sample", 10, false)
        history.Append("alpha")
        history.Append("beta")
        let source = "[[;teal;]formatted]"
        staticHelpers.Value <-
            sprintf "plugin version: %s\nstrip: %s\nhas formatting: %b\nvalid color 'red': %b\nparsed command: %s\narguments: %s\nstandalone history: %s\nactive terminal: %s"
                Terminal.PluginVersion
                (Terminal.Strip(source))
                (Terminal.HaveFormatting(source))
                (Terminal.ValidColor("red"))
                (if isNull parsed.Name then "-" else parsed.Name)
                args
                (history.Data() |> String.concat " | ")
                (if Optional.isDefined active then "yes" else "no")

    // ---------------------------------------------------------------
    // Interpreter
    // ---------------------------------------------------------------

    let commandHelp =
        [ "help                 — this list"
          "echo <text>          — echo through the interpreter"
          "raw                  — echo HTML markup"
          "clear                — clear the terminal"
          "date                 — current date/time"
          "prompt <text>        — change the prompt"
          "history              — print the command history"
          "state                — export the terminal view"
          "push                 — push the nested interpreter"
          "pop                  — pop back to this interpreter"
          "level                — print the interpreter level"
          "notify               — show a browser notification (graceful fallback)"
          "" ]

    let nestedInterpreter =
        ThisFunc<Terminal, string, Terminal, obj>(fun (this: Terminal) command _term ->
            match (if isNull command then "" else command.Trim()) with
            | "pop" ->
                this.Pop() |> ignore
                null
            | "clear" ->
                this.Clear() |> ignore
                null
            | "level" ->
                this.Echo("level " + string (this.Level())) |> ignore
                null
            | _ ->
                this.Echo("nested> type 'pop' to return") |> ignore
                null)

    let interpreter =
        ThisFunc<Terminal, string, Terminal, obj>(fun (this: Terminal) command term ->
            let command = if isNull command then "" else command.Trim()
            match command with
            | "" -> null
            | "help" ->
                this.Echo(commandHelp |> String.concat "\n") |> ignore
                null
            | "clear" ->
                this.Clear() |> ignore
                null
            | "date" ->
                this.Echo(nowString ()) |> ignore
                null
            | "raw" ->
                this.EchoHtml("<span style=\"color:#0f766e\">raw HTML</span> · <a href=\"https://terminal.jcubic.pl\" target=\"_blank\" rel=\"noreferrer\">jQuery Terminal</a>") |> ignore
                null
            | "history" ->
                let items = this.History().Data()
                if items.Length = 0 then this.Echo("(empty)") |> ignore
                else items |> Array.iter (fun item -> this.Echo(item) |> ignore)
                null
            | "state" ->
                let view = this.ExportView()
                output.Value <-
                    sprintf "view: command=%s, position=%d, focus=%b"
                        (if isNull view.Command then "" else view.Command)
                        view.Position
                        view.Focus
                null
            | "push" ->
                let options = PushOptions()
                options.Name <- "nested"
                options.Prompt <- Union2Of2 "nested> "
                this.Push(nestedInterpreter, options) |> ignore
                status.Value <- "Pushed a nested interpreter."
                null
            | "pop" ->
                this.Pop() |> ignore
                null
            | "level" ->
                this.Echo("level " + string (this.Level())) |> ignore
                null
            | "notify" ->
                status.Value <-
                    if JS.Window?Notification ===. JS.Undefined then
                        "Notification API is not available in this browser."
                    else
                        "Notification API is available; the sample does not request permission."
                null
            | cmd when cmd.StartsWith "echo " ->
                this.Echo(command.Substring(5)) |> ignore
                null
            | cmd when cmd.StartsWith "prompt " ->
                this.SetPrompt(command.Substring(7)) |> ignore
                null
            | unknown ->
                term.Error("Unknown command: " + unknown) |> ignore
                null)

    let createTerminal () =
        let options = TerminalOptions()
        options.Name <- "WebSharperTerminal"
        options.Prompt <- Union2Of2 "> "
        options.Greetings <-
            Union2Of2 "Welcome to the WebSharper jQuery Terminal sample!\nType 'help' for the list of commands."
        options.History <- true
        options.HistorySize <- 200
        options.Exit <- false
        options.CheckArity <- false
        options.ConvertLinks <- true
        options.LinksNoReferrer <- true
        options.ScrollOnEcho <- true
        options.OnInit <-
            ThisFunc<Terminal, Terminal, bool>(fun (this: Terminal) _term ->
                this.Enable() |> ignore
                true)
        let t = Terminal("#terminal", interpreter, options)
        terminal <- Some t
        version.Value <- t.Version()
        status.Value <- "Terminal " + t.Version() + " created from npm jquery.terminal."
        refreshState ()

    // ---------------------------------------------------------------
    // Toolbar actions
    // ---------------------------------------------------------------

    let echo () =
        withTerminal (fun t ->
            t.Echo("Echoed at " + nowString ()) |> ignore
            status.Value <- "Echo wrote a line."
            refreshState ())

    let echoHtml () =
        withTerminal (fun t ->
            t.EchoHtml("<b>bold</b> · <i>italic</i> · <code>code</code>") |> ignore
            status.Value <- "EchoHtml wrote HTML."
            refreshState ())

    let clear () =
        withTerminal (fun t ->
            t.Clear() |> ignore
            status.Value <- "Cleared the terminal."
            refreshState ())

    let reset () =
        withTerminal (fun t ->
            t.Reset() |> ignore
            status.Value <- "Reset the terminal."
            refreshState ())

    let setPrompt () =
        withTerminal (fun t ->
            t.SetPrompt("\u00bb ") |> ignore
            status.Value <- "Prompt set to »."
            refreshState ())

    let insertText () =
        withTerminal (fun t ->
            t.Insert("inserted text", true) |> ignore
            status.Value <- "Inserted text into the command line."
            refreshState ())

    let execCommand () =
        withTerminal (fun t ->
            let command = commandText.Value
            if command.Trim() = "" then
                status.Value <- "Type a command first."
            else
                t.Exec(command) |> ignore
                status.Value <- "Executed: " + command
                refreshState ())

    let pushInterpreter () =
        withTerminal (fun t ->
            let options = PushOptions()
            options.Name <- "nested"
            options.Prompt <- Union2Of2 "nested> "
            t.Push(nestedInterpreter, options) |> ignore
            status.Value <- "Pushed a nested interpreter."
            refreshState ())

    let popInterpreter () =
        withTerminal (fun t ->
            t.Pop() |> ignore
            status.Value <- "Popped the interpreter stack."
            refreshState ())

    let pauseTerminal () =
        withTerminal (fun t ->
            t.Pause() |> ignore
            status.Value <- "Terminal paused."
            refreshState ())

    let resumeTerminal () =
        withTerminal (fun t ->
            t.Resume() |> ignore
            status.Value <- "Terminal resumed."
            refreshState ())

    let freezeTerminal () =
        withTerminal (fun t ->
            t.Freeze() |> ignore
            status.Value <-
                if t.Frozen() then "Terminal frozen (input blocked)."
                else "Terminal unfrozen."
            refreshState ())

    let disableTerminal () =
        withTerminal (fun t ->
            if t.Enabled() then
                t.Disable() |> ignore
                status.Value <- "Terminal disabled."
            else
                t.Enable() |> ignore
                status.Value <- "Terminal enabled."
            refreshState ())

    let showOutput () =
        withTerminal (fun t ->
            let text = t.GetOutput()
            let lines = t.GetOutput(true)
            output.Value <-
                if isNull text || text.Trim() = "" then "(empty)"
                else text.Trim()
            status.Value <-
                sprintf "%d output line(s), %d character(s)."
                    (if isNull lines then 0 else lines.Length)
                    (if isNull text then 0 else text.Length))

    let showHistory () =
        withTerminal (fun t ->
            let items = t.History().Data()
            historyText.Value <-
                if isNull items || items.Length = 0 then "(empty)"
                else items |> Array.truncate 8 |> String.concat " · "
            output.Value <-
                if items.Length = 0 then "(history is empty)"
                else items |> String.concat "\n"
            status.Value <- "Read the command history."
            refreshState ())

    let exportView () =
        withTerminal (fun t ->
            let view = t.ExportView()
            output.Value <-
                sprintf "command: %s\nposition: %d\nfocus: %b\nhistory entries: %d"
                    (if isNull view.Command then "" else view.Command)
                    view.Position
                    view.Focus
                    (if isNull view.History then 0 else view.History.Length)
            status.Value <- "Exported the terminal view."
            refreshState ())

    let probeApi () =
        withTerminal (fun t ->
            let report = ResizeArray<string>()
            let check name (f: unit -> string) =
                try report.Add(name + ": " + f ())
                with ex -> report.Add(name + ": ERROR " + ex.Message)
            check "signature" (fun () -> t.Signature())
            check "rows x cols" (fun () -> sprintf "%d x %d" (t.Rows()) (t.Cols()))
            check "history size" (fun () -> string (t.History().Data().Length))
            check "is_bottom" (fun () -> string (t.IsBottom()))
            check "last_index" (fun () -> string (t.LastIndex()))
            check "cmd name" (fun () -> t.Cmd().Name())
            check "cmd position" (fun () -> string (t.Cmd().Position()))
            check "cmd display_position" (fun () -> string (t.Cmd().DisplayPosition()))
            check "cmd column" (fun () -> string (t.Cmd().Column(true)))
            check "cmd isenabled" (fun () -> string (t.Cmd().IsEnabled()))
            check "cmd history" (fun () -> string (t.Cmd().History().Data().Length))
            check "cmd refresh" (fun () -> t.Cmd().Refresh() |> ignore; "ok")
            check "cmd set/insert/kill" (fun () ->
                t.Cmd().Set("probe").Insert("!").KillText() |> ignore
                t.Cmd().Set("") |> ignore
                "ok")
            check "cmd clear_cache" (fun () -> t.Cmd().ClearCache() |> ignore; "ok")
            check "signal" (fun () -> if isNull (t.Signal()) then "null" else "ok")
            check "token" (fun () -> if Optional.isDefined (t.Token()) then "present" else "(none)")
            check "login_name" (fun () -> if Optional.isDefined (t.LoginName()) then "present" else "(none)")
            check "prefix_name" (fun () -> t.PrefixName())
            check "get/set command" (fun () ->
                t.SetCommand("probe")
                let c = t.GetCommand()
                t.SetCommand("") |> ignore
                c)
            check "get/set position" (fun () ->
                let p = t.GetPosition()
                t.SetPosition(p) |> ignore
                string p)
            check "set_mask" (fun () -> t.SetMask(false) |> ignore; "ok")
            check "flush" (fun () -> t.Flush() |> ignore; "ok")
            check "scroll_to_bottom" (fun () -> t.ScrollToBottom() |> ignore; "ok")
            check "complete" (fun () -> string (t.Complete([| "help" |])))
            check "save_state" (fun () -> t.SaveState() |> ignore; "ok")
            check "export/import view" (fun () ->
                let v = t.ExportView()
                t.ImportView(v) |> ignore
                "ok")
            check "resize" (fun () -> t.Resize(60, 26) |> ignore; "ok")
            output.Value <- String.concat "\n" report
            status.Value <- "API tour ran through Cmd, signal, tokens, geometry, view and resize."
            refreshState ())

    // ---------------------------------------------------------------
    // Entry point
    // ---------------------------------------------------------------

    [<SPAEntryPoint>]
    let Main () =
        // Install the plugin explicitly once; constructors and the static
        // helpers reuse the cached jQuery instance afterwards.
        Terminal.Load()

        // Restore the theme before the template is bound, so the first paint
        // already has the stored palette.
        try
            match JS.Window.LocalStorage.GetItem(themeKey) with
            | "light" | "dark" as stored -> theme.Value <- stored
            | _ -> ()
        with _ ->
            ()
        applyTheme ()

        IndexTemplate()
            .themeLabel(themeLabel.View)
            .status(status.View)
            .output(output.View)
            .version(version.View)
            .geometry(geometry.View)
            .level(level.View)
            .settings(settingsText.View)
            .history(historyText.View)
            .staticHelpers(staticHelpers.View)
            .commandText(commandText)
            .toggleTheme(fun _ -> toggleTheme ())
            .echo(fun _ -> echo ())
            .echoHtml(fun _ -> echoHtml ())
            .clear(fun _ -> clear ())
            .reset(fun _ -> reset ())
            .setPrompt(fun _ -> setPrompt ())
            .insertText(fun _ -> insertText ())
            .execCommand(fun _ -> execCommand ())
            .pushInterpreter(fun _ -> pushInterpreter ())
            .popInterpreter(fun _ -> popInterpreter ())
            .pauseTerminal(fun _ -> pauseTerminal ())
            .resumeTerminal(fun _ -> resumeTerminal ())
            .freezeTerminal(fun _ -> freezeTerminal ())
            .disableTerminal(fun _ -> disableTerminal ())
            .refreshState(fun _ -> refreshState ())
            .showOutput(fun _ -> showOutput ())
            .showHistory(fun _ -> showHistory ())
            .exportView(fun _ -> exportView ())
            .probeApi(fun _ -> probeApi ())
            .Bind()

        // The template is in the DOM now, so the `#terminal` container exists.
        try
            createTerminal ()
            refreshStaticHelpers ()
        with ex ->
            status.Value <- "Could not create the terminal: " + ex.Message
            output.Value <- string ex.Message