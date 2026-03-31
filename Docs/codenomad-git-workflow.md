# CodeNomad Git Workflow

This repo is set up to work cleanly with CodeNomad using the local clone as the root workspace.

## Current approach

- Remote auth uses HTTPS with GitHub CLI credentials.
- The main repo stays at `D:\LIVE PROJECTS\TheLineup\The Lineup`.
- Extra task branches should use Git worktrees under `D:\LIVE PROJECTS\TheLineup\worktrees`.

## Local Git defaults

- `fetch.prune=true` keeps remote branch references tidy.
- `pull.ff=only` prevents accidental merge commits during pull.
- `push.autoSetupRemote=true` auto-wires a new branch to `origin` on first push.
- `rerere.enabled=true` remembers conflict resolutions.

## Recommended task flow

1. Keep `master` clean in the main workspace.
2. For each new task, create a new branch worktree.
3. Open that worktree in CodeNomad for isolated changes.
4. Review Git Changes before each commit.
5. Push the branch and open a pull request when ready.

## Useful commands

```bash
git fetch --all --prune
git worktree add "D:/LIVE PROJECTS/TheLineup/worktrees/feature-name" -b feature/feature-name origin/master
git worktree list
git status
git push
```

## Cleanup

After a branch is merged:

```bash
git worktree remove "D:/LIVE PROJECTS/TheLineup/worktrees/feature-name"
git branch -d feature/feature-name
git fetch --prune
```
