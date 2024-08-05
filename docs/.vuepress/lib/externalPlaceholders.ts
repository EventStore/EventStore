const externalPlaceholders = ["@samples", "@clients"];

export function isKnownPlaceholder(placeholder: string): boolean {
    const known = externalPlaceholders.find(x => x == placeholder);
    return known !== undefined;
}
