const versionRegex = /v((\d+\.)?(\d+\.)?(\*|\d+))/;
const nightly = "nightly";
const v = {
    isVersion: (v: string) => versionRegex.test(v),
    parseVersion: (v: string) => versionRegex.exec(v),
    getVersion: (path: string): string | undefined => {
        if (path.includes(nightly)) {
            return nightly;
        }
        const ref = path.split("#")[0];
        const split = ref.split("/");
        return split.find(x => v.isVersion(x));
    }
};
export default v;
