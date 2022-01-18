export function setStorageValue(prefix, name, value) {
    if (typeof localStorage === "undefined") {
        return;
    }
    localStorage[prefix + name] = value;
}

export function getStorageValue(prefix, name) {
    if (typeof localStorage === "undefined") {
        return null;
    }
    name = prefix + name;
    if (!localStorage[name]) {
        return null;
    }
    return localStorage[name];
}
