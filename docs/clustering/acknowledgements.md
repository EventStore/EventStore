# Acknowledgements

By default, every write to the cluster needs to be acknowledged by all cluster members. This condition could be relaxed to speed up the writes, but it comes with a risk of data loss.

We do not advise to change the acknowledgement settings.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--commit-count` |
| YAML                 | `CommitCount` |
| Environment variable | `EVENTSTORE_COMMIT_COUNT` |

**Default**: `-1`, all nodes must acknowledge commits.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--prepare-count` |
| YAML                 | `PrepareCount` |
| Environment variable | `EVENTSTORE_PREPARE_COUNT` |

**Default**: `-1`, all nodes must acknowledge prepares.
