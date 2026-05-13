create table if not exists validation_runs
(
    id serial primary key,
    created_at timestamptz not null default now()
);
