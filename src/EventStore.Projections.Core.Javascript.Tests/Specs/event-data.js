fromAll().when(
{
    $init: function() {
        return { events: [] };
    },
    "baz" : function(state, event){
        state.events.push(event);
    }
})
