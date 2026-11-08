using SQLite;

public sealed class Migration007BotSkillRating : IDatabaseMigration
{
    public int Version => 7;
    public string Name => "bot_skill_rating";
    public string Checksum => "v7-bot-skill-rating";

    public void Apply(SQLiteConnection connection)
    {
        if (!DatabaseHealth.TableExists(connection, "bot_state"))
            return;

        if (!DatabaseHealth.ColumnExists(connection, "bot_state", "skill"))
        {
            connection.Execute("ALTER TABLE bot_state ADD COLUMN skill REAL;");
        }

        connection.Execute(@"
            UPDATE bot_state
            SET skill = CASE UPPER(COALESCE(difficulty, 'Easy'))
                WHEN 'MEDIUM' THEN 4.0
                WHEN 'HARD' THEN 7.0
                WHEN 'PRO' THEN 10.0
                ELSE 1.0
            END
            WHERE skill IS NULL;");
    }
}
