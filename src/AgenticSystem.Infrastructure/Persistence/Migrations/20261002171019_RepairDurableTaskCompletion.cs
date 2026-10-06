using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairDurableTaskCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION dt.complete_tasks(
                    p_completed_sequence_numbers BIGINT[], p_results dt.task_result[])
                RETURNS BIGINT[] LANGUAGE plpgsql AS $$
                DECLARE
                    v_task_hub VARCHAR(50);
                    v_deleted BIGINT[];
                    v_expected INTEGER;
                BEGIN
                    v_task_hub := dt.current_task_hub();
                    v_expected := COALESCE(cardinality(p_completed_sequence_numbers), 0);
                    -- Lock actual instance rows, not a DISTINCT projection. Ordering
                    -- makes overlapping batches acquire instance locks consistently.
                    PERFORM 1 FROM dt.instances i
                    WHERE i.task_hub = v_task_hub
                      AND i.runtime_status IN ('Running', 'Suspended')
                      AND EXISTS (SELECT 1 FROM unnest(p_results) r
                          WHERE r.instance_id = i.instance_id AND r.execution_id = i.execution_id)
                    ORDER BY i.instance_id FOR UPDATE OF i;

                    -- Stale results must not enter another execution or a terminal instance.
                    INSERT INTO dt.payloads (task_hub, instance_id, payload_id, text, reason)
                    SELECT v_task_hub, r.instance_id, r.payload_id, r.payload_text, r.reason
                    FROM unnest(p_results) r
                    INNER JOIN dt.instances i ON i.task_hub = v_task_hub
                        AND i.instance_id = r.instance_id AND i.execution_id = r.execution_id
                        AND i.runtime_status IN ('Running', 'Suspended')
                    WHERE r.payload_id IS NOT NULL;

                    INSERT INTO dt.new_events (task_hub, instance_id, execution_id, name,
                        event_type, task_id, visible_time, payload_id, trace_context)
                    SELECT v_task_hub, r.instance_id, r.execution_id, r.name,
                        r.event_type, r.task_id, r.visible_time, r.payload_id, r.trace_context
                    FROM unnest(p_results) r
                    INNER JOIN dt.instances i ON i.task_hub = v_task_hub
                        AND i.instance_id = r.instance_id AND i.execution_id = r.execution_id
                        AND i.runtime_status IN ('Running', 'Suspended');

                    WITH deleted AS (
                        DELETE FROM dt.new_tasks
                        WHERE task_hub = v_task_hub
                            AND sequence_number = ANY(p_completed_sequence_numbers)
                        RETURNING sequence_number)
                    SELECT COALESCE(array_agg(sequence_number ORDER BY sequence_number), ARRAY[]::BIGINT[])
                    INTO v_deleted FROM deleted;
                    IF cardinality(v_deleted) <> v_expected THEN
                        RAISE EXCEPTION 'Failed to delete all task events. Expected %, got %.',
                            v_expected, cardinality(v_deleted) USING ERRCODE = '40001';
                    END IF;
                    RETURN v_deleted;
                END;
                $$;
                COMMENT ON FUNCTION dt.complete_tasks IS
                    'Atomically complete task batches and publish only current active-execution results';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Signature is backward-compatible. A schema downgrade retains this repair
            // instead of restoring a function PostgreSQL cannot execute.
        }
    }
}
