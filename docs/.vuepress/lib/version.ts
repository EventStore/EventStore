const versionRegex = /(v?)((\d+\.)?(\d+\.)?(\*|\d+))/;
export default {
    isVersion: (v: string) => versionRegex.test(v),
    parseVersion: (v: string) => versionRegex.exec(v)
}
