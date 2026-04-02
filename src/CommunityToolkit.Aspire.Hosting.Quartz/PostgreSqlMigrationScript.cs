namespace CommunityToolkit.Aspire.Hosting.Quartz;

public static class PostgreSqlMigrationScript
{
    public const string Script = @"
-- Quartz.NET PostgreSQL Schema

CREATE TABLE qrtz_job_details (
    sched_name VARCHAR(120) NOT NULL,
    job_name VARCHAR(150) NOT NULL,
    job_group VARCHAR(150) NOT NULL,
    description VARCHAR(250) NULL,
    job_class_name VARCHAR(250) NOT NULL,
    is_durable BOOLEAN NOT NULL,
    is_nonconcurrent BOOLEAN NOT NULL,
    is_update_data BOOLEAN NOT NULL,
    requests_recovery BOOLEAN NOT NULL,
    job_data BYTEA NULL,
    PRIMARY KEY (sched_name, job_name, job_group)
);

CREATE TABLE qrtz_triggers (
    sched_name VARCHAR(120) NOT NULL,
    trigger_name VARCHAR(150) NOT NULL,
    trigger_group VARCHAR(150) NOT NULL,
    job_name VARCHAR(150) NOT NULL,
    job_group VARCHAR(150) NOT NULL,
    description VARCHAR(250) NULL,
    next_fire_time BIGINT NULL,
    prev_fire_time BIGINT NULL,
    priority INTEGER NULL,
    trigger_state VARCHAR(16) NOT NULL,
    trigger_type VARCHAR(8) NOT NULL,
    start_time BIGINT NOT NULL,
    end_time BIGINT NULL,
    calendar_name VARCHAR(200) NULL,
    misfire_instr INTEGER NULL,
    job_data BYTEA NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, job_name, job_group) REFERENCES qrtz_job_details(sched_name, job_name, job_group)
);

CREATE TABLE qrtz_simple_triggers (
    sched_name VARCHAR(120) NOT NULL,
    trigger_name VARCHAR(150) NOT NULL,
    trigger_group VARCHAR(150) NOT NULL,
    repeat_count INTEGER NOT NULL,
    repeat_interval BIGINT NOT NULL,
    times_triggered INTEGER NOT NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
);

CREATE TABLE qrtz_cron_triggers (
    sched_name VARCHAR(120) NOT NULL,
    trigger_name VARCHAR(150) NOT NULL,
    trigger_group VARCHAR(150) NOT NULL,
    cron_expression VARCHAR(250) NOT NULL,
    time_zone_id VARCHAR(80) NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
);

CREATE TABLE qrtz_fired_triggers (
    sched_name VARCHAR(120) NOT NULL,
    entry_id VARCHAR(140) NOT NULL,
    trigger_name VARCHAR(150) NOT NULL,
    trigger_group VARCHAR(150) NOT NULL,
    instance_name VARCHAR(200) NOT NULL,
    fired_time BIGINT NOT NULL,
    sched_time BIGINT NOT NULL,
    priority INTEGER NOT NULL,
    state VARCHAR(16) NOT NULL,
    job_name VARCHAR(150) NULL,
    job_group VARCHAR(150) NULL,
    is_nonconcurrent BOOLEAN NULL,
    requests_recovery BOOLEAN NULL,
    PRIMARY KEY (sched_name, entry_id)
);

CREATE TABLE qrtz_scheduler_state (
    sched_name VARCHAR(120) NOT NULL,
    instance_name VARCHAR(200) NOT NULL,
    last_checkin_time BIGINT NOT NULL,
    checkin_interval BIGINT NOT NULL,
    PRIMARY KEY (sched_name, instance_name)
);

CREATE TABLE qrtz_locks (
    sched_name VARCHAR(120) NOT NULL,
    lock_name VARCHAR(40) NOT NULL,
    PRIMARY KEY (sched_name, lock_name)
);

CREATE TABLE qrtz_calendars (
    sched_name VARCHAR(120) NOT NULL,
    calendar_name VARCHAR(200) NOT NULL,
    calendar BYTEA NOT NULL,
    PRIMARY KEY (sched_name, calendar_name)
);

CREATE TABLE qrtz_paused_trigger_grps (
    sched_name VARCHAR(120) NOT NULL,
    trigger_group VARCHAR(150) NOT NULL,
    PRIMARY KEY (sched_name, trigger_group)
);

CREATE TABLE qrtz_blob_triggers (
    sched_name VARCHAR(120) NOT NULL,
    trigger_name VARCHAR(150) NOT NULL,
    trigger_group VARCHAR(150) NOT NULL,
    blob_data BYTEA NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
);

-- Idempotency table for duplicate job prevention
CREATE TABLE qrtz_idempotency_keys (
    idempotency_key VARCHAR(200) NOT NULL PRIMARY KEY,
    job_id VARCHAR(150) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    expires_at TIMESTAMP NOT NULL
);

-- Performance indexes
CREATE INDEX idx_qrtz_t_next_fire_time ON qrtz_triggers(sched_name, next_fire_time);
CREATE INDEX idx_qrtz_t_state ON qrtz_triggers(sched_name, trigger_state);
CREATE INDEX idx_qrtz_t_nft_st ON qrtz_triggers(sched_name, trigger_state, next_fire_time);
CREATE INDEX idx_qrtz_ft_trig_inst_name ON qrtz_fired_triggers(sched_name, instance_name);
CREATE INDEX idx_qrtz_ft_inst_job_req_rcvry ON qrtz_fired_triggers(sched_name, instance_name, requests_recovery);
CREATE INDEX idx_qrtz_idempotency_expires ON qrtz_idempotency_keys(expires_at);
";
}
