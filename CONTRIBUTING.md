# Contributing to EventStoreDB

## Working with the Git

We're using `master` as the main development branch. It contains all changes to the upcoming release. Older releases have dedicated feature branches with the format `release/oss-{version}`. E.g. `release/oss-v5`, `release/oss-v20.10`, `release/oss-v21.2`. Specific releases are tagged from the release branches commits.

We attempt to do our best to ensure that the history remains clean and to do so, we generally ask contributors to squash their commits into a set or single logical commit.

To contribute to EventStoreDB:

1. Fork the repository.
2. Create a feature branch from the `master` (or release) branch.
3. It's recommended using rebase strategy for feature branches (see more in [Git documentation](https://git-scm.com/book/en/v2/Git-Branching-Rebasing)). Having that, we highly recommend using clear commit messages. Commits should also represent the unit of change.
4. Before sending PR to ensure that you rebased the latest source branch from the main repository.
5. When you're ready to create the [Pull Request on GitHub](https://github.com/EventStore/EventStore/compare).

## Documentation

Documentation is located in the [`docs`](/docs) folder. It's orchestrated in the separate [documentation repository](https://github.com/EventStore/documentation). It's available online at https://developers.eventstore.com/.

It's recommended to have documentation changes be put together with code changes.

We're supporting multiple versions of the documentation. Versions are kept in:
- the main (`master`) branch: all changes that refer to the upcoming release should be put there. That includes both non-released changes and enhancements to documentation for existing features.
- specific release branches - the last version and older release are kept there (e.g. `release/oss-v5`, `release/oss-v20.10`, `release/oss-v21.2`). We aim to keep the up to date documentation for the last LTS releases and all further. Read more on the release strategy: [link](https://www.eventstore.com/blog/eventstoredb-20.10-lts-has-been-released).

To update the specific database version's docs, it's recommended to create a feature branch based on the particular version release branch. For instance, if you want to change documentation in the `24.2` version, then you should:
- checkout the latest `release/oss-v24.2`,
- create a new branch and add your changes,
- create pull request targeting the `release/oss-v24.2` branch.

If you're unsure which branch to select, the safe choice is the main branch (`master`).

It's not needed to send multiple pull requests if your change should be reflected in multiple database versions documentation. Contributors reviewing the pull request should mark it with proper labels (e.g. as `cherry-pick:release/oss-v23.10`). We're using the [GitHub action](/.github/workflows/cherry-pick-pr-for-label.yml) based on the labels that should create pull requests with cherry-picks to the target branches. Action will inform about success or failure via the review comments to the initial pull request tagging the person that merged the pull request. It's recommended for contributors to monitor those notifications and make sure that cherry-picks succeeded. Read more in [action documentation](https://github.com/EventStore/Automations/tree/master/cherry-pick-pr-for-label).

Taking the previous example. You sent a pull request targeting the `release/oss-v24.2`. You'd like to have it also for the upcoming release and version `23.10`. Contributor should label your pull request with:
- `cherry-pick:master`,
- `cherry-pick:release/oss-v23.10`.

_**Note:** Cherry-pick action requires changes to be rebased. If there is a merge commit, then cherry-pick will fail. It will also fail if there is a conflict with the target branch (so `target_branch` from label suffix). If those cases happen then, it's needed to do manual cherry-picks._

## Code style

Coding rules are set up in the [.editorconfig file](/src/.editorconfig). This file is supported by all popular IDE (e.g. Microsoft Visual Studio, Rider, Visual Studio Code). Unless you disabled it manually, it should be automatically applied after opening the solution. We also recommend turning automatic formatting on saving to have all the rules applied.

## Licensing and legal rights

By contributing to EventStoreDB:

1. You assert that contribution is your original work.
2. You assert that you have the right to assign the copyright for the work.
3. You are accepting the [License](LICENSE.md).
