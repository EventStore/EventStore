export const normalizeWhitespace = (input: string): string => {
    // empty lines before start of snippet
    const trimmed = input.replace(/^([ 	]*\n)*/, '');

    // find the shortest indent at the start of line
    const shortest = trimmed.match(/^[ 	]*(?=\S)/gm)?.reduce((a, b) => a.length <= b.length ? a : b) ?? '';

    if (shortest.length) {
        // remove the shortest indent from each line (if exists)
        return trimmed.replace(new RegExp(`^${shortest}`, 'gm'), '');
    }

    return trimmed;
}
