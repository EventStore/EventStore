create table if not exists event_type (
    id int4 primary key,
    name varchar,
    unique(name)
);

create table if not exists category (
    id int4 primary key,
    name varchar,
    unique(name)
);

create table if not exists streams (
    id ubigint primary key,
    name varchar,
    unique(name),
    max_age int4,
    max_count int4
);

create table if not exists idx_all (
    seq ubigint,
    event_number int4,
    log_position ubigint,
    created timestamp,
    stream ubigint,
    event_type int4,
    event_type_seq int8,
    category int4,
    category_seq int8,
	foreign key(stream) references streams(id),
	foreign key(event_type) references event_type(id),
	foreign key(category) references category(id)
);

create index if not exists idx_all_category on idx_all(category, category_seq);
