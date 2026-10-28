#!/usr/bin/env python3
"""Teamleiter-Sperre: Die Hauptsitzung (Teamleiter) ändert keine Dateien im Repository.

Aufruf als PreToolUse-Hook (siehe .claude/settings.json). Liest das Hook-Input als JSON
von stdin. Hat es ein `agent_id`, läuft der Aufruf in einem Mitglied (Subagent): dann
passiert nichts. Sonst ist es der Teamleiter, und diese Aufrufe werden abgelehnt:
  1. Edit, Write, NotebookEdit auf Dateien unter /home/claude/projekte/ (Repository und
     alle Worktrees, auch $CLAUDE_PROJECT_DIR und `git worktree list`).
  2. Bash mit schreibendem git-Unterkommando (commit, merge, rebase, reset, cherry-pick,
     revert, am, apply, add, rm, mv, restore, stash, tag, checkout, switch) und mit
     `git push` (außer `git push ... --delete ...`), dazu `sed -i` und `perl -i`
     auf Pfade unter /home/claude/projekte/.
Erlaubt bleiben u. a. git fetch/log/show/diff/status/branch/worktree/pull --ff-only
und alle gh-Befehle.

GRENZEN (ehrlich): Die Sperre prüft nur Text. Mit Shell-Umwegen (Umleitung `> datei`,
`tee`, `python -c`, `bash -c`, Variablen, Skripte) lässt sie sich umgehen. Sie fängt
Versehen, keine Absicht. Bei unlesbarem Input oder Fehlern blockiert sie nie (Exit 0),
damit nichts hängt.

Selbsttest: python3 .claude/hooks/teamleiter_sperre.py --selbsttest
"""
import json
import os
import re
import subprocess
import sys

BASIS = "/home/claude/projekte/"
GRUND_DATEI = "Teamleiter ändert keine Dateien im Repository – Auftrag an ein Mitglied"
GRUND_BASH = "Teamleiter ändert keine Dateien im Repository und committet nicht – Auftrag an ein Mitglied"
SCHREIBENDE_GIT = {
    "commit", "merge", "rebase", "reset", "cherry-pick", "revert", "am", "apply",
    "add", "rm", "mv", "restore", "stash", "tag", "checkout", "switch",
}
# git-Optionen, die vor dem Unterkommando stehen dürfen (mit Wert)
GIT_OPT_MIT_WERT = {"-C", "-c", "--git-dir", "--work-tree", "--namespace"}


def ordner_des_projekts():
    """Projektordner und alle Worktrees (für Pfade außerhalb von BASIS)."""
    ordner = [BASIS]
    pd = os.environ.get("CLAUDE_PROJECT_DIR")
    if pd:
        ordner.append(os.path.realpath(pd).rstrip("/") + "/")
        try:
            aus = subprocess.run(
                ["git", "-C", pd, "worktree", "list", "--porcelain"],
                capture_output=True, text=True, timeout=5).stdout
            for zeile in aus.splitlines():
                if zeile.startswith("worktree "):
                    ordner.append(os.path.realpath(zeile[9:]).rstrip("/") + "/")
        except Exception:
            pass
    return ordner


def im_repository(pfad):
    if not pfad:
        return False
    p = os.path.realpath(os.path.expanduser(pfad))
    p = p.rstrip("/") + "/"
    return any(p.startswith(o) for o in ordner_des_projekts())


def git_befehle(befehl):
    """Liefert je git-Aufruf (Unterkommando, restliche Wörter)."""
    ergebnis = []
    for teil in re.split(r"&&|\|\||;|\||\n", befehl):
        woerter = teil.split()
        # nur wenn git das erste Wort des Teilbefehls ist (nicht in Texten von gh ... --body)
        for i, w in enumerate(woerter[:1]):
            if w == "git" or w.endswith("/git"):
                j = i + 1
                while j < len(woerter) and woerter[j].startswith("-"):
                    j += 2 if woerter[j] in GIT_OPT_MIT_WERT else 1
                if j < len(woerter):
                    ergebnis.append((woerter[j], woerter[j + 1:]))
    return ergebnis


def pruefe_bash(befehl):
    for sub, rest in git_befehle(befehl):
        if sub == "push":
            if not any(r == "--delete" or r == "-d" for r in rest):
                return GRUND_BASH
        elif sub in SCHREIBENDE_GIT:
            # Ausnahmen: harmlose Lesefälle
            if sub == "stash" and rest[:1] in (["list"], ["show"]):
                continue
            if sub == "tag" and (not rest or rest[0] in ("-l", "--list")):
                continue
            return GRUND_BASH
    for teil in re.split(r"&&|\|\||;|\||\n", befehl):
        woerter = teil.split()
        if not woerter:
            continue
        # sed -i / perl -i (auch -pi, -i.bak, --in-place)
        if woerter[0] in ("sed", "perl") or any(w in ("sed", "perl") for w in woerter[:3]):
            if any(re.fullmatch(r"-[A-Za-z]*i[A-Za-z.]*|--in-place(=.*)?", w) for w in woerter[1:]) \
                    and BASIS in teil:
                return GRUND_BASH
    return None


def entscheide(daten):
    """Gibt den Ablehnungsgrund zurück oder None."""
    if daten.get("agent_id"):
        return None
    name = daten.get("tool_name")
    eingabe = daten.get("tool_input") or {}
    if name in ("Edit", "Write", "NotebookEdit"):
        pfad = eingabe.get("file_path") or eingabe.get("notebook_path")
        if im_repository(pfad):
            return GRUND_DATEI
    elif name == "Bash":
        return pruefe_bash(eingabe.get("command") or "")
    return None


def main():
    try:
        daten = json.load(sys.stdin)
        grund = entscheide(daten)
    except Exception:
        return 0
    if grund:
        print(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": grund}}))
    return 0


def selbsttest():
    r = BASIS + "reichsgilde/"
    faelle = [
        ("Mitglied darf schreiben", {"agent_id": "a1", "tool_name": "Edit", "tool_input": {"file_path": r + "x.cs"}}, False),
        ("Edit im Repository", {"tool_name": "Edit", "tool_input": {"file_path": r + "x.cs"}}, True),
        ("Write im Worktree", {"tool_name": "Write", "tool_input": {"file_path": BASIS + "rg-foo/y.md"}}, True),
        ("Write im Notizordner", {"tool_name": "Write", "tool_input": {"file_path": "/tmp/claude-1000/scratchpad/n.md"}}, False),
        ("Write im Memory", {"tool_name": "Write", "tool_input": {"file_path": "/home/claude/.claude/projects/p/memory/a.md"}}, False),
        ("NotebookEdit im Repo", {"tool_name": "NotebookEdit", "tool_input": {"notebook_path": r + "a.ipynb"}}, True),
        ("git commit", {"tool_name": "Bash", "tool_input": {"command": "git commit -m x"}}, True),
        ("git -C merge", {"tool_name": "Bash", "tool_input": {"command": "git -C /x merge origin/main"}}, True),
        ("Kette mit add", {"tool_name": "Bash", "tool_input": {"command": "cd a && git add -A"}}, True),
        ("git push", {"tool_name": "Bash", "tool_input": {"command": "git push origin main"}}, True),
        ("git push --delete", {"tool_name": "Bash", "tool_input": {"command": "git push origin --delete foo"}}, False),
        ("git fetch", {"tool_name": "Bash", "tool_input": {"command": "git fetch origin"}}, False),
        ("git log", {"tool_name": "Bash", "tool_input": {"command": "git log origin/main --oneline -15"}}, False),
        ("git pull --ff-only", {"tool_name": "Bash", "tool_input": {"command": "git pull --ff-only"}}, False),
        ("git branch -D", {"tool_name": "Bash", "tool_input": {"command": "git branch -D foo"}}, False),
        ("git worktree add", {"tool_name": "Bash", "tool_input": {"command": "git worktree add ../rg-x -b y"}}, False),
        ("Mitglied git commit", {"agent_id": "a1", "tool_name": "Bash", "tool_input": {"command": "git commit -m x"}}, False),
        ("gh pr merge", {"tool_name": "Bash", "tool_input": {"command": "gh pr merge 5 --merge --delete-branch"}}, False),
        ("sed -i im Repo", {"tool_name": "Bash", "tool_input": {"command": "sed -i 's/a/b/' " + r + "x.cs"}}, True),
        ("perl -pi im Repo", {"tool_name": "Bash", "tool_input": {"command": "perl -pi -e 's/a/b/' " + r + "x.cs"}}, True),
        ("sed -i außerhalb", {"tool_name": "Bash", "tool_input": {"command": "sed -i 's/a/b/' /tmp/x.txt"}}, False),
        ("sed ohne -i", {"tool_name": "Bash", "tool_input": {"command": "sed -n 1,5p " + r + "x.cs"}}, False),
    ]
    fehler = 0
    for titel, daten, erwartet in faelle:
        ist = entscheide(daten) is not None
        ok = ist == erwartet
        fehler += not ok
        print(("ok     " if ok else "FEHLER ") + titel + (" (abgelehnt)" if ist else " (erlaubt)"))
    # unlesbarer Input blockiert nie
    p = subprocess.run([sys.executable, __file__], input="kein json", capture_output=True, text=True)
    ok = p.returncode == 0 and p.stdout == ""
    fehler += not ok
    print(("ok     " if ok else "FEHLER ") + "unlesbarer Input (erlaubt)")
    print("%d Fälle, %d Fehler" % (len(faelle) + 1, fehler))
    return 1 if fehler else 0


if __name__ == "__main__":
    sys.exit(selbsttest() if "--selbsttest" in sys.argv else main())
