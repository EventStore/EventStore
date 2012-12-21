function renderHtmlBy(data, templateJs) {

    $.when($.get(templateJs))
        .done(function(template) {
            $.templates({ renderTemplate: template });
            $('#data').html($.render.renderTemplate(data,
                {
                    formatDate: function(s) {
                        var d = new Date(s);
                        return d.toString();
                    }
                }
            ));

        });
}