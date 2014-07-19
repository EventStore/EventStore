Event Store Projections Upgrade Tool (v1.x -> 2.0.0)
----------------------------------------------------

This tool can be used to upgrade a version 1.x Event Store database which contains
projections in order to make it compatible with version 2.0.0 of the Event Store.

This is necessary as several aspects of projections have changed between major versions,
including the names of the streams in which they are defined (they are now prefixed with
a $ sign in the same way as other internal streams.

After running this tool, projections will resume processing from the same checkpoint
they had reached under version 1.x of Event Store.

Usage
-----

With the database attached to Event Store 1.x:

1) Stop all projections using the web UI (Projections tab) or via calls to the HTTP
   endpoint. This forces checkpoints for each projection to be written.

2) Attach the database to Event Store 2.0.0, by running using the "--db ./mydata"
   command line attribute. Do NOT turn on user projections, however system projections
   need to be switched on. The command line will likely look like this:

      ./EventStore.SingleNode.exe --run-projections=system --db ./mydatabase

With the database attached to Event Store 2.x:

3) Run the upgrade tool. The command line options are:

	--ip    - the IP address of the v2.0.0 Event Store
	--port	- the TCP endpoint of the v2.0.0 Event Store
	--user	- the username of an account with $admin permissions
	--password - the password of the user account

   If you are running the upgrade tool on the same machine as the server, a typical
   call will look like this:
   
        .\UpgradeProjections.exe --ip 127.0.0.1 --port 1113 --user admin --password changeit

   The tool will report the changes it will make, but will not make any changes at
   this point.

3) Re-run the projection upgrade tool with the "--upgrade" flag if the output of the
   previous step looks reasonable and does not report any errors.

4) Restart the Event Store 2.0.0 server, allowing user projections to run by
   using the "--run-projections=all" command line parameter.

5) Enable projections one-by-one watching for any errors in the log files.


Support
-------

If you have a commercial support contract for Event Store, please report any issues
encountered during upgrade via the commercial support channel.

If you are using the OSS version of Event Store, please report any issues encountered
during upgrade to the mailing list (http://groups.google.com/group/event-store).
