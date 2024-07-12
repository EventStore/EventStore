import { defineClientAppEnhance } from '@vuepress/client';
import PrimeVue from "primevue/config";
import "primeicons/primeicons.css";
import "primeflex/primeflex.css";
import "primevue/resources/primevue.css";
import "./styles/prime-theme.css";
import TabView from "primevue/tabview";
import TabPanel from "primevue/tabpanel";
import Card from "primevue/card";
import XodeBlock from "./components/xode/XodeBlock.vue";
import XodeGroup from "./components/xode/XodeGroup";

export default defineClientAppEnhance(({ app, router, siteData }) => {
    // PrimeVue components
    app.use(PrimeVue);
    app.component("TabView", TabView);
    app.component("TabPanel", TabPanel);
    app.component("Card", Card);

    // Code block components
    delete app._context.components["CodeGroup"];
    delete app._context.components["CodeGroupItem"];
    app.component("XodeBlock", XodeBlock);
    app.component("XodeGroup", XodeGroup);
    app.component(XodeBlock.name, XodeBlock);
    app.component(XodeGroup.name, XodeGroup);
})
