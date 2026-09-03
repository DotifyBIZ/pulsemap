## [1.1.1](https://github.com/DotifyBIZ/pulsemap/compare/v1.1.0...v1.1.1) (2026-09-03)


### Bug Fixes

* **app:** highlight hoverable walls, merge test-point suggestions into Suggestions tab ([62621ac](https://github.com/DotifyBIZ/pulsemap/commit/62621acf52e0e39a8d94d4e37200e843a663e854))
* close dead ends, crash paths, and untrusted-input gaps found in a full app review ([f38b795](https://github.com/DotifyBIZ/pulsemap/commit/f38b795d0db9810ebd6dee712b06ba8ffbb0fbaa))

# [1.1.0](https://github.com/DotifyBIZ/pulsemap/compare/v1.0.0...v1.1.0) (2026-09-02)


### Bug Fixes

* **app:** close out remaining UX-review polish items ([15f96c7](https://github.com/DotifyBIZ/pulsemap/commit/15f96c77838be3c222b9580e6d1c72fb7b51c140))
* **workspace:** add single-level undo for the Delete tool ([60c23df](https://github.com/DotifyBIZ/pulsemap/commit/60c23df28923dc881eaac3ea12ea53650e65e57e))
* **workspace:** replace Pivot with TabView to fix tab-header clipping ([2fc361f](https://github.com/DotifyBIZ/pulsemap/commit/2fc361f753829e5532303fa020ab983cfd45dc72))


### Features

* **app:** in-app update check against GitHub Releases ([d2242ad](https://github.com/DotifyBIZ/pulsemap/commit/d2242adddbfdb3d73f68c91a4aa39f08efc15328))
* **app:** Workspace onboarding/tool help, and a warmer Home greeting ([d308432](https://github.com/DotifyBIZ/pulsemap/commit/d3084323bcb4462deed104cb92e74c73292a129d))
* **core:** coordinate AP channel assignment across floors ([0f93258](https://github.com/DotifyBIZ/pulsemap/commit/0f93258623c09346d9bc176e4a4e6070cbc32431))
* WLAN link diagnostics, warmer Home messages, and a warmer Home ([8a1bf00](https://github.com/DotifyBIZ/pulsemap/commit/8a1bf0096242350c2a9316ab8c77fa63abcae260))
* **workspace:** drag-to-resize/reposition outdoor area bounds ([9cfcc65](https://github.com/DotifyBIZ/pulsemap/commit/9cfcc65bdc61521d1da432035d40ede001c37fdb))
* **workspace:** per-wall material editing and guided-walk improvements ([9875403](https://github.com/DotifyBIZ/pulsemap/commit/98754037da54c34197bb751bf934cacc03a262d1))

# 1.0.0 (2026-09-02)


### Bug Fixes

* **app:** create the log folder before opening it from Settings ([4659cc2](https://github.com/DotifyBIZ/pulsemap/commit/4659cc2f019c3b2f6489a2c05773e096506f78e1))
* **ci:** map Pulsemap.App's Any CPU solution config to x64, not x86 ([a23b755](https://github.com/DotifyBIZ/pulsemap/commit/a23b755880ff21513721201ecf5f6b9f61d5e3ed))
* **ci:** regenerate packages.lock.json after the Platform/RID fix ([8eff720](https://github.com/DotifyBIZ/pulsemap/commit/8eff7201a7b0c034032374c91a59b5567fd79ef6))
* **ci:** turn off PublishReadyToRun, template default breaks CI with NETSDK1094 ([5f7606e](https://github.com/DotifyBIZ/pulsemap/commit/5f7606e70fa0aa483cc748566b9b646612285fdf))
* **core:** harden persistence, export, and RF math against malformed input ([0b1f7dc](https://github.com/DotifyBIZ/pulsemap/commit/0b1f7dcaa14243f0790a48f37d7eade063c52323))


### Features

* **app:** add a Diagnostics section to Settings for the local log file ([cc5366e](https://github.com/DotifyBIZ/pulsemap/commit/cc5366eba8bc43b30ddfd0019b042b9064d13dc7))
* **app:** add a Surveys library page, trim Home to a dashboard ([2e1dc0a](https://github.com/DotifyBIZ/pulsemap/commit/2e1dc0adad8411150bd91394a8b31af772a547c2))
* **app:** add DI, NavigationView shell, design tokens, and HomePage ([d3fa3d2](https://github.com/DotifyBIZ/pulsemap/commit/d3fa3d2b2f66bfe02d0d50cc158d1390af82cd57))
* **app:** add guided measurement walk ([9618cca](https://github.com/DotifyBIZ/pulsemap/commit/9618cca0b506bece1533304ae78941062a485f3e))
* **app:** add native WLAN scanning and wire it into the Workspace ([5c1c9ac](https://github.com/DotifyBIZ/pulsemap/commit/5c1c9acffd1551f0ede84a004a9c6fa31bf7a148))
* **app:** add New Survey wizard ([0a01c98](https://github.com/DotifyBIZ/pulsemap/commit/0a01c989d0d850530daff83c345470b56c028daf))
* **app:** add real PL+EN localization infrastructure and a Settings page ([f7535bd](https://github.com/DotifyBIZ/pulsemap/commit/f7535bd252a5e92fe1fd2758597ed9b1ab39469b)), closes [C#-built](https://github.com/C/issues/-built)
* **app:** add Workspace page and FloorPlanCanvas ([4456dde](https://github.com/DotifyBIZ/pulsemap/commit/4456dde11115cd0732c0b0b98f9bdcce1f0d1b20))
* **app:** migrate wizard and Workspace strings to the localization pipeline ([b92eec1](https://github.com/DotifyBIZ/pulsemap/commit/b92eec1e9ad9173812617f90014d6c199d38344b))
* **app:** render the floor plan image/PDF background on the Workspace canvas ([743e4e1](https://github.com/DotifyBIZ/pulsemap/commit/743e4e170bf557c0d694fb207b406a83264272c0))
* **app:** replace placeholder app icon and set the executable icon ([cb8a5f2](https://github.com/DotifyBIZ/pulsemap/commit/cb8a5f2a4bd6139b08ae2cc46cc2e174d7bdce30))
* **app:** wire CSV/JSON/PDF export up to real UI ([db6629a](https://github.com/DotifyBIZ/pulsemap/commit/db6629ad2eeaf90cb40d1719df0ad4468f777348))
* **app:** Workspace UI for floors/outdoor areas, and a snapshot comparison page ([7e408e1](https://github.com/DotifyBIZ/pulsemap/commit/7e408e123a662810cfa0322827f063e9f3033c37))
* **core:** add CSV/JSON raw data export and PDF coverage report ([58083a9](https://github.com/DotifyBIZ/pulsemap/commit/58083a9dd447b46a3dff2411c0631b36742d12ef))
* **core:** add greedy AP placement optimizer and channel plan ([d35ee19](https://github.com/DotifyBIZ/pulsemap/commit/d35ee190ca8a46abc20bbe13b41b1192cfda6f3c))
* **core:** add local troubleshooting log file (not telemetry) ([2cae26d](https://github.com/DotifyBIZ/pulsemap/commit/2cae26dc9e4c0e3157d6244f8f7103ff6724c151))
* **core:** add log-distance propagation model with wall attenuation ([f654839](https://github.com/DotifyBIZ/pulsemap/commit/f65483941ac391510a715b0c4d2df302906d1863))
* **core:** add ordinary kriging interpolation for coverage heatmaps ([92e5666](https://github.com/DotifyBIZ/pulsemap/commit/92e56664af1a1154ee77b2eca7700c0934f12ecb))
* **core:** add survey domain models and zip+JSON file persistence ([debcb74](https://github.com/DotifyBIZ/pulsemap/commit/debcb740a93bf642af571d9e05505a74da4413bf))
* **core:** make AP placement's coverage-reliability threshold measurement-aware ([ee8ba15](https://github.com/DotifyBIZ/pulsemap/commit/ee8ba151e9ed978bdf9c0dc6e6f4dfb5a9c39a78))
* **core:** multi-floor, outdoor areas, inter-floor propagation, adaptive Kriging suggestion ([2d7c00a](https://github.com/DotifyBIZ/pulsemap/commit/2d7c00af95005a058a9626dcac97e4a44b4e110b))
* **core:** rank AP channel suggestions by measured interference ([3d26645](https://github.com/DotifyBIZ/pulsemap/commit/3d266454fbfaf623e663461c59bd3cca934c4ef1))

# Changelog

All notable changes to this project are documented here, generated automatically by [semantic-release](https://semantic-release.gitbook.io/) from [Conventional Commits](https://www.conventionalcommits.org/) on every merge to `main`.

Do not edit this file by hand — changes will be overwritten on the next release.

Nothing has been released yet.
