$(document).ready(function () {
    const fileManager = $("#filemanager").getKendoFileManager();

    const uploadButton = $("#filemanager").find('*[data-command="OpenDialogCommand"]');
    const backupButton = $('<a role="button" href="" tabindex="0" class="k-button k-button-icontext" data-overflow="auto" aria-disabled="false"><span class="k-icon k-i-cloud"></span>Backups</a>');
    uploadButton.after(backupButton);

    backupButton.click(function () {
        BackupsPrompt();
    })

    //Context Menu
    let contextmenu = $("ul[data-role='contextmenu']");
    const backupItem = $('<li class="k-item k-menu-item k-state-default" data-command="BackupCommand" role="menuitem" data-uid="undefined"><span class="k-link k-menu-link"><span class="k-icon k-i-cloud"></span>Backup</span></li>');
    const downloadItem = contextmenu.find('*[data-command="DownloadCommand"]');

    fileManager.contextMenu.bind("open", function (e) {
        downloadItem.after(backupItem);
        let selected = fileManager.getSelected();
        if (selected.length === 1 && selected[0].extension === ".zip") {
            backupItem.show();
        } else {
            backupItem.hide();
        }
    });
})