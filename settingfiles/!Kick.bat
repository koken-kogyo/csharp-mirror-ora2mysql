set yyyyMMdd=%date:/=%
MirrorOra2MySQL.exe 7 /D > %yyyyMMdd%_MirrorOra2MySQL_Details.log
MirrorOra2MySQL.exe 7 /E > %yyyyMMdd%_MirrorOra2MySQL.log
