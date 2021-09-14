# Server

## Server settings

EventStoreDB server settings allow you to tweak the behaviour of the database server during the startup and at run-time. Usually, you'd want to change these settings to solve some performance issues if they occur.

- [Database settings](./database.md) define where the database files are stored, what part of the database can be cached and how the server executes transactions.

- [Threading](threading.md) settings allow you to adjust the number of threads that EventStoreDB uses for different operations. We provide reasonable defaults for those settings, change them with caution. Increasing the thread count won't necessarily improve the system performance.

- [HTTP Caching](./caching.md) is enabled by default. Learn more about it before changing the setting.

## Default directories

Depending on the platform and installation type, the location of EventStoreDB executables, configuration and other necessary files vary.

Check the [default directories](default-directories.md) documentation page to find out where EventStoreDB keeps its files.
