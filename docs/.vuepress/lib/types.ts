import type {NavItemOptions, SidebarLinkOptions} from "vuepress-theme-hope";

export interface EsSidebarGroupOptions extends NavItemOptions {
    group?: string;
    collapsible?: boolean;
    title?: string;
    version?: string;
    prefix?: string;
    link?: string;
    children: EsSidebarItemOptions[] | string[];
}

export type EsSidebarItemOptions = SidebarLinkOptions | EsSidebarGroupOptions | string;
export type EsSidebarArrayOptions = EsSidebarItemOptions[];
export type EsSidebarObjectOptions = Record<string, EsSidebarArrayOptions | "structure">;
export type EsSidebarOptions = EsSidebarArrayOptions | EsSidebarObjectOptions;

export type ImportedSidebarArrayOptions = EsSidebarGroupOptions[];