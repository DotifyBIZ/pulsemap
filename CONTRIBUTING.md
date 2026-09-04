# Contributing to Pulsemap

Thanks for considering a contribution. Pulsemap is early — expect things to move and shift.

This file covers *process* — branching, commits, the PR checklist. For how the codebase is actually organized, how to get it running, and how the propagation/placement engine works, see the **[Developer Guide](docs/developer-guide.md)**.

## Before you start

- Read the [Code of Conduct](CODE_OF_CONDUCT.md).
- Read [CLAUDE.md](CLAUDE.md) — the project's engineering rules and a running log of platform-specific gotchas already hit once; no need to hit them again.
- For anything nontrivial, open an issue first to talk through the approach before writing code. Saves everyone rework.
- Contributions are accepted under the project's [PolyForm Shield 1.0.0 license](LICENSE) — by submitting a change, you agree it's licensed on the same terms as the rest of the project.

## Branching and commits

We use trunk-based development: `main` is always deployable, work happens on short-lived branches.

**Branch naming:** `<type>/<short-description>`, kebab-case, e.g. `feat/add-kriging-interpolation`, `fix/heatmap-render-glitch`.

**Commits** follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short summary>
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Subject line ≤72 characters, imperative mood ("add", not "added"), no trailing period. `CHANGELOG.md` is generated automatically from these — don't edit it by hand.

**No AI attribution.** If you use an AI tool to help write a change, that's fine — but don't add `Co-Authored-By` trailers naming an AI model, or "Generated with"-style tags, to commits, PR descriptions, or code comments. You're the author and the one accountable for the change, regardless of what tools you used to get there.

## Before opening a pull request

- [ ] Branch is up to date with `main`
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet build --configuration Release` and `dotnet test --configuration Release` pass
- [ ] New business logic in `Pulsemap.App.Core` has tests
- [ ] Commits follow Conventional Commits; PR title matches `type(scope): summary`
- [ ] User-facing changes are documented (usually in [docs/user-guide.md](docs/user-guide.md)); architecture/behavior changes affecting contributors are reflected in [docs/developer-guide.md](docs/developer-guide.md)
- [ ] No `Co-Authored-By` trailers or other AI attribution

## Code review

Reviewers label comments **[blocking]**, **[suggestion]**, or **[nit]**. Every open comment gets addressed or discussed before a re-review. See [docs/adr/](docs/adr/) if your change touches an existing architectural decision — either follow it or propose a new ADR explaining why not.

## Questions

Open an issue, tag it `question`.
