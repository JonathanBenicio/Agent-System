BEGIN;
DO $probe$
DECLARE
    hub TEXT := dt.current_task_hub();
    instance TEXT := 'review-completion-' || gen_random_uuid()::TEXT;
    execution TEXT := gen_random_uuid()::TEXT;
    ids BIGINT[];
    deleted BIGINT[];
    results dt.task_result[];
    n INTEGER;
BEGIN
    IF cardinality(dt.complete_tasks(ARRAY[]::BIGINT[], ARRAY[]::dt.task_result[])) <> 0 THEN
        RAISE EXCEPTION 'Empty batch did not return an empty array';
    END IF;
    INSERT INTO dt.instances(task_hub,instance_id,execution_id,name,runtime_status)
        VALUES(hub,instance,execution,'ReviewCompletion','Running');
    FOREACH n IN ARRAY ARRAY[1,2,4] LOOP
        WITH added AS (
            INSERT INTO dt.new_tasks(task_hub,instance_id,execution_id,name,task_id)
            SELECT hub,instance,execution,'ReviewTask',item FROM generate_series(1,n) item
            RETURNING sequence_number)
        SELECT array_agg(sequence_number ORDER BY sequence_number) INTO ids FROM added;
        SELECT array_agg(ROW(instance,execution,'ReviewTask','TaskCompleted',item,NULL,
            '{"safe":"fixture"}'::JSONB,gen_random_uuid(),NULL,NULL)::dt.task_result)
        INTO results FROM generate_series(1,n) item;
        deleted := dt.complete_tasks(ids,results);
        IF cardinality(deleted) <> n THEN RAISE EXCEPTION 'Wrong deletion count for batch %',n; END IF;
        IF (SELECT count(*) FROM dt.new_events WHERE task_hub=hub AND instance_id=instance) <> n THEN
            RAISE EXCEPTION 'Wrong event count for batch %',n;
        END IF;
        DELETE FROM dt.new_events WHERE task_hub=hub AND instance_id=instance;
        DELETE FROM dt.payloads WHERE task_hub=hub AND instance_id=instance;
    END LOOP;
    INSERT INTO dt.new_tasks(task_hub,instance_id,execution_id,name,task_id)
        VALUES(hub,instance,execution,'RetryProbe',99) RETURNING ARRAY[sequence_number] INTO ids;
    results := ARRAY[ROW(instance,execution,'RetryProbe','TaskCompleted',99,NULL,
        '{"safe":"fixture"}'::JSONB,gen_random_uuid(),NULL,NULL)::dt.task_result];
    BEGIN
        PERFORM dt.complete_tasks(ids || ARRAY[-1::BIGINT],results);
        RAISE EXCEPTION 'Missing task must reject the entire completion';
    EXCEPTION WHEN serialization_failure THEN NULL;
    END;
    IF NOT EXISTS(SELECT 1 FROM dt.new_tasks WHERE task_hub=hub AND sequence_number=ANY(ids)) OR
       EXISTS(SELECT 1 FROM dt.new_events WHERE task_hub=hub AND instance_id=instance) OR
       EXISTS(SELECT 1 FROM dt.payloads WHERE task_hub=hub AND instance_id=instance) THEN
        RAISE EXCEPTION 'Failed completion left partial tasks/events/payloads';
    END IF;
    UPDATE dt.instances SET runtime_status='Completed' WHERE task_hub=hub AND instance_id=instance;
    PERFORM dt.complete_tasks(ids,results);
    IF EXISTS(SELECT 1 FROM dt.new_events WHERE task_hub=hub AND instance_id=instance) THEN
        RAISE EXCEPTION 'Terminal instance received a stale event';
    END IF;
END;
$probe$;
ROLLBACK;
