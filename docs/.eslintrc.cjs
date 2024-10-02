module.exports = {
    root: true,
    extends: 'vuepress',
    overrides: [
        {
            files: ['*.ts', '*.vue'],
            extends: 'vuepress-typescript',
            parserOptions: {
                project: ['tsconfig.json'],
            },
        },
    ],
}