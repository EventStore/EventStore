import {reactive} from "vue";
import {getUrlParamValue} from "../../lib/url";
import {getStorageValue, setStorageValue} from "../../lib/localStorage";

const prefix = "eventstore-docs";
const langStorageName = "codeLanguage";

function getSelectedCodeLanguage() {
    return getUrlParamValue(langStorageName) || getStorageValue(prefix, langStorageName) || "";
}

export default {
    state: reactive({
        connectionString: "",
        codeLanguage: getSelectedCodeLanguage()
    }),
    changeLanguage(language) {
        this.state.codeLanguage = language;
        setStorageValue(prefix, langStorageName, language);
    },
    updateConnectionString(conn) {
        this.state.connectionString = conn;
    }
}
