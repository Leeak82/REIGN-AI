---
name: "powershell-command-syntax-prevention"
description: "Prevent PowerShell parser syntax errors when combining commands"
---

Use separate command lines for cd and command execution to prevent PowerShell parser errors.

Steps:
1. Execute cd command to change directory
2. Wait for command completion
3. Execute subsequent commands on separate lines
4. Never combine cd with && or other operators in single command
5. Use PowerShell proper command chaining: cd "path"; command
6. Use Windows-style path separators with double quotes
7. Validate directory exists before executing commands

Evidence:
Trajectory showed parser errors when combining cd && commands

Common failure: cd "C:\\path" && Get-ChildItem
Correct: cd "C:\\path"; Get-ChildItem

Bundle: powershell-command-chaining
