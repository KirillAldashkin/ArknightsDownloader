# Preparing
Go to **"Actions"** and download latest version for your OS + CPU, then unpack it
# Using
Start executable and follow instructions in terminal.
## Actions
### Download latest version of resources
Download the most recent version for a chosen server. By default, all versions are 
stored at the same time, but **<u>files from previous versions are reused</u>** using
symlinks, so disk usage is not a problem.

Assets are downloaded to `assets/{server}/{version}/resources`  
This action can be paused (press Ctrl+C **once** and wait a bit) and resumed
on the next app launch (if no version was released during this pause).
> [!NOTE]
> This is resilient to network failures - it will crash but on the
> next launch it will continue downloading from where it stopped.
### Delete old versions
Cleanup by deleting one of:
* One version chosen by you
* For every server, all versions except for the most recent one
## Format of `config.json`
* `folder` - where all results are located
* `platform` - how to present yourself to the game's servers. Only tested value is `Android`
* `threads` - enables mutltithreaded downloading. Too small values will result in incomplete 
network utilization, too big - in spontaneous time out errors. Default value of `3` works
fine for ~100Mbit/s connection
* `lpackPreference` - uhm... I'm not sure how to explain this properly, just leave default
value.
* `servers` - list of game's servers, all known ones are added by default.
  * `key` - used as a folder name
  * `name` - displayed in program
  * `url` - server's location