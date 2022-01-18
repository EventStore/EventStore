import {path} from '@vuepress/utils';
import {defineUserConfig} from "@vuepress/cli";
import type {DefaultThemeOptions} from "@vuepress/theme-default";
import containers from "./lib/containers";
import {importCodePlugin} from "./markdown/xode/importCodePlugin";
import {resolveMultiSamplesPath} from "./lib/samples";
import {linkCheckPlugin} from "./markdown/linkCheck";
import {replaceLinkPlugin} from "./markdown/replaceLink";

export default defineUserConfig<DefaultThemeOptions>({
    title: "EventStoreDB Documentation",
    description: "The stream database built for Event Sourcing",
    clientAppEnhanceFiles: path.resolve(__dirname, './clientAppEnhance.ts'),
    markdown: {importCode: false},
    extendsMarkdown: md => {
        md.use(importCodePlugin, {
            handleImportPath: s => resolveMultiSamplesPath(s)
        });
        md.use(linkCheckPlugin);
        md.use(replaceLinkPlugin, {
            replaceLink: (link: string, _) => link
                .replace("@server", "")
                .replace("@clients/http-api/", "/http-api/")
                .replace("@http-api", "../../samples/http-api")
        });
    },
    themeConfig: {
        sidebarDepth: 2,
        docsDir: ".",
        sidebar: {
            "/": require("../sidebar"),
            "/http-api/": require("../http-api/sidebar")
        },
        navbar: [
            {
                text: "Server",
                link: "/",
            },
            {
                text: "HTTP API",
                link: "/http-api/"
            }
        ]
    },
    plugins: [
        containers("tabs", "TabView", type => `${type ? ` type='${type}'` : ""}`),
        containers("tab", "TabPanel", label => `header="${label}"`),
        ["@vuepress/container", {
            type: "note",
            before: title => `<div class="custom-container note"><p class="custom-container-title">${title === "" ? "NOTE" : title}</p>`,
            after: _ => `</div>`
        }],
        ["@vuepress/container", {
            type: "card",
            before: _ => `<Card><template #content>`,
            after: _ => `</template></Card>`
        }]
    ],
});
