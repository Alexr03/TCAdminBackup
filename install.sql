create table tcmodule_backups
(
    backupId     int  not null
        primary key,
    serviceId    int  not null,
    fileServerId int  not null,
    fileName     text null,
    backupType   int  not null,
    app_data     text not null
);

