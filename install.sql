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

INSERT INTO tcadmin.tc_site_map (page_id, module_id, parent_page_id, parent_page_module_id, category_id, url, mvc_url, controller, action, display_name, page_small_icon, panelbar_icon, show_in_sidebar, view_order, required_permissions, menu_required_permissions, page_manager, page_search_provider, cache_name) VALUES (NULL, '07405876-e8c2-4b24-a774-4ef57f596384', null, null, 4, '/BackupAdmin', '/BackupAdmin', 'BackupAdmin', 'Index', 'Backup Administration', 'MenuIcons/Base/Config24x24.png', 'MenuIcons/Base/Config16x16.png', 1, 1000, '({07405876-e8c2-4b24-a774-4ef57f596384,0,0})', '({07405876-e8c2-4b24-a774-4ef57f596384,0,0})', null, null, '');