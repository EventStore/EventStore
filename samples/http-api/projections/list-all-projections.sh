curl -i http://localhost:2113/projections/any \
    -H "accept:application/json" -u "admin:changeit" \
     | grep -E 'effectiveName|status'
