curl -i --data-binary "@xbox-one-s-counter.js" \
    http://localhost:2113/projections/continuous?name=xbox-one-s-counter%26type=js%26enabled=true%26emit=true%26trackemittedstreams=true \
    -u admin:changeit
