using System.Globalization;
using Microsoft.Data.Sqlite;
using SeeSharp.Content;
using SeeSharp.Core;

namespace SeeSharp.Progress;

/// <param name="PassedItems">Item ids with a passing attempt.</param>
/// <param name="Passed">Every item of the exercise has a passing attempt.</param>
/// <param name="MissingSkills">Required skills not yet held at least <see cref="SkillState.Practiced"/>; empty means unlocked.</param>
public sealed record ExerciseProgress(Exercise Exercise, IReadOnlySet<string> PassedItems, bool Passed, IReadOnlyList<string> MissingSkills)
{
    public bool Unlocked => MissingSkills.Count == 0;
}

/// <summary>A module unlocks when any of its exercises does.</summary>
public sealed record ModuleProgress(Module Module, IReadOnlyList<ExerciseProgress> Exercises)
{
    public bool Unlocked => Exercises.Any(e => e.Unlocked);
    public bool Passed => Exercises.Count > 0 && Exercises.All(e => e.Passed);
}

public sealed record CourseProgress(IReadOnlyList<ModuleProgress> Modules, IReadOnlyDictionary<string, SkillStatus> HeldSkills)
{
    public IEnumerable<ExerciseProgress> Exercises => Modules.SelectMany(m => m.Exercises);
}

/// <summary>
/// SQLite-backed attempt log and skill ledger. Append-only; every "current state" is derived by
/// <see cref="ProgressDerivation"/> from the rows. One trainee, one file, one connection.
/// </summary>
public sealed class ProgressStore : IDisposable
{
    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeeSharp", "progress.db");

    // Append a script per schema change; never edit a published one. PRAGMA user_version tracks how many ran.
    private static readonly string[] Migrations =
    [
        """
        CREATE TABLE attempt (
            id            INTEGER PRIMARY KEY,
            exercise_id   TEXT    NOT NULL,
            item_id       TEXT    NOT NULL,
            timestamp     TEXT    NOT NULL,
            time_spent_ms INTEGER NOT NULL,
            passed        INTEGER NOT NULL,
            answer        TEXT,
            source        BLOB,
            grading_json  TEXT,
            trx           TEXT);
        CREATE INDEX attempt_item ON attempt(exercise_id, item_id);
        CREATE TABLE skill_transition (
            id        INTEGER PRIMARY KEY,
            skill_id  TEXT    NOT NULL,
            state     INTEGER NOT NULL,
            timestamp TEXT    NOT NULL,
            cause     TEXT    NOT NULL);
        """,
        // Task 8: exam runs; their attempts carry the run id and stay out of regular progress.
        """
        CREATE TABLE exam_run (
            id        INTEGER PRIMARY KEY,
            exam_id   TEXT    NOT NULL,
            real      INTEGER NOT NULL,
            started   TEXT    NOT NULL,
            deadline  TEXT    NOT NULL,
            seed      INTEGER NOT NULL,
            items     TEXT    NOT NULL,
            submitted TEXT);
        ALTER TABLE attempt ADD COLUMN exam_run_id INTEGER REFERENCES exam_run(id);
        """,
    ];

    private readonly SqliteConnection _connection;
    private readonly object _lock = new(); // ponytail: one connection + lock; fine for one trainee and one browser

    public string Path { get; }

    /// <param name="path">Database file; created with its directory when missing. Defaults to <see cref="DefaultPath"/>.</param>
    public ProgressStore(string? path = null)
    {
        Path = path ?? DefaultPath;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false }.ToString());
        _connection.Open();
        Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private void Migrate()
    {
        lock (_lock)
        {
            var version = Convert.ToInt32(Execute("PRAGMA user_version").ExecuteScalar(), CultureInfo.InvariantCulture);
            for (var i = version; i < Migrations.Length; i++)
            {
                using var tx = _connection.BeginTransaction();
                Execute(Migrations[i]).ExecuteNonQuery();
                Execute($"PRAGMA user_version = {i + 1}").ExecuteNonQuery();
                tx.Commit();
            }
        }
    }

    private SqliteCommand Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    // ----- writes -----------------------------------------------------------------------------

    /// <summary>
    /// Appends the attempt and returns it with its id. No ledger update; use the overload with the manifest for that.
    /// An attempt with <see cref="Attempt.ExamRunId"/> is refused once that run is submitted or its deadline has passed.
    /// </summary>
    public Attempt RecordAttempt(Attempt attempt)
    {
        lock (_lock)
        {
            if (attempt.ExamRunId is { } runId && ExamRun(runId) is not { Active: true })
                throw new InvalidOperationException($"Exam run {runId} is not active.");
            var id = (long)Execute("""
                INSERT INTO attempt (exercise_id, item_id, timestamp, time_spent_ms, passed, answer, source, grading_json, trx, exam_run_id)
                VALUES ($exercise, $item, $timestamp, $spent, $passed, $answer, $source, $grading, $trx, $run);
                SELECT last_insert_rowid();
                """,
                ("$exercise", attempt.ExerciseId), ("$item", attempt.ItemId), ("$timestamp", Iso(attempt.Timestamp)),
                ("$spent", (long)attempt.TimeSpent.TotalMilliseconds), ("$passed", attempt.Passed ? 1 : 0),
                ("$answer", attempt.Answer), ("$source", attempt.Source), ("$grading", attempt.GradingJson), ("$trx", attempt.Trx),
                ("$run", attempt.ExamRunId)).ExecuteScalar()!;
            return attempt with { Id = id };
        }
    }

    /// <summary>
    /// Appends the attempt and, when every item of <paramref name="exercise"/> now has a passing attempt, credits its skills:
    /// <c>teaches</c> become <see cref="SkillState.Practiced"/>, <c>requires</c> become <see cref="SkillState.Applied"/> (SKILL-MODEL.md §2).
    /// </summary>
    public Attempt RecordAttempt(Attempt attempt, ExerciseManifest exercise)
    {
        if (attempt.ExerciseId != exercise.Id)
            throw new ArgumentException($"Attempt is for '{attempt.ExerciseId}', manifest is '{exercise.Id}'.", nameof(exercise));
        lock (_lock)
        {
            var recorded = RecordAttempt(attempt);
            if (!attempt.Passed || !exercise.Items.All(i => PassedItems(exercise.Id).Contains(i.Id)))
                return recorded;
            var held = HeldSkills();
            Append(ProgressDerivation.Transitions(exercise.Teaches, SkillState.Practiced, exercise.Id, attempt.Timestamp, held));
            Append(ProgressDerivation.Transitions(exercise.Requires, SkillState.Applied, exercise.Id, attempt.Timestamp, held));
            return recorded;
        }
    }

    /// <summary>The trainee read a module summary: every skill its exercises teach becomes <see cref="SkillState.Introduced"/>.</summary>
    public void MarkSummaryRead(Module module, DateTimeOffset? timestamp = null)
    {
        lock (_lock)
        {
            var skills = module.Exercises.SelectMany(e => e.Manifest.Teaches).Distinct(StringComparer.Ordinal);
            Append(ProgressDerivation.Transitions(skills, SkillState.Introduced, module.Manifest.Id, timestamp ?? DateTimeOffset.UtcNow, HeldSkills()));
        }
    }

    private void Append(IEnumerable<SkillTransition> transitions)
    {
        foreach (var t in transitions)
            Execute("INSERT INTO skill_transition (skill_id, state, timestamp, cause) VALUES ($skill, $state, $timestamp, $cause)",
                ("$skill", t.SkillId), ("$state", (int)t.State), ("$timestamp", Iso(t.Timestamp)), ("$cause", t.Cause)).ExecuteNonQuery();
    }

    // ----- exam runs (task 8) -----------------------------------------------------------------

    /// <summary>
    /// Opens a run: the deadline is <paramref name="now"/> + <paramref name="timeLimit"/> and persisted before anything is shown.
    /// Refused while another run is open, and for a real exam that already has a run — a real exam is taken once per id.
    /// </summary>
    public ExamRun StartExamRun(string examId, bool real, TimeSpan timeLimit, int seed, IReadOnlyList<ExamItemRef> items, DateTimeOffset? now = null)
    {
        var start = now ?? DateTimeOffset.UtcNow;
        lock (_lock)
        {
            if (ActiveRun(start) is { } open)
                throw new InvalidOperationException($"Exam run {open.Id} ({open.ExamId}) is still open.");
            if (real && ExamRuns(examId, start).Count > 0)
                throw new InvalidOperationException($"Real exam '{examId}' was already taken.");
            var id = (long)Execute("""
                INSERT INTO exam_run (exam_id, real, started, deadline, seed, items) VALUES ($exam, $real, $started, $deadline, $seed, $items);
                SELECT last_insert_rowid();
                """,
                ("$exam", examId), ("$real", real ? 1 : 0), ("$started", Iso(start)), ("$deadline", Iso(start + timeLimit)),
                ("$seed", seed), ("$items", string.Join("\n", items.Select(i => i.ExerciseId + "/" + i.ItemId)))).ExecuteScalar()!;
            return ExamRun(id)!;
        }
    }

    /// <summary>Closes the run; later attempts for it are refused. Submitting twice is a no-op.</summary>
    public void SubmitExamRun(long id, DateTimeOffset? now = null)
    {
        lock (_lock)
            Execute("UPDATE exam_run SET submitted = $now WHERE id = $id AND submitted IS NULL",
                ("$now", Iso(now ?? DateTimeOffset.UtcNow)), ("$id", id)).ExecuteNonQuery();
    }

    /// <summary>The one open run, or null. Expired runs are submitted first (at their deadline), so the timer needs no background job.</summary>
    public ExamRun? ActiveRun(DateTimeOffset? now = null) => ExamRuns(null, now).FirstOrDefault(r => r.Active);

    public ExamRun? ExamRun(long id) => ReadRuns("WHERE id = $id", ("$id", id)).SingleOrDefault();

    /// <summary>All runs, newest last, optionally for one exam. Expires overdue runs first.</summary>
    public IReadOnlyList<ExamRun> ExamRuns(string? examId = null, DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            Execute("UPDATE exam_run SET submitted = deadline WHERE submitted IS NULL AND deadline <= $now", ("$now", Iso(now ?? DateTimeOffset.UtcNow))).ExecuteNonQuery();
            return ReadRuns("WHERE $exam IS NULL OR exam_id = $exam", ("$exam", examId));
        }
    }

    private List<ExamRun> ReadRuns(string where, params (string Name, object? Value)[] parameters)
    {
        lock (_lock)
        {
            using var reader = Execute($"SELECT id, exam_id, real, started, deadline, seed, items, submitted FROM exam_run {where} ORDER BY id", parameters).ExecuteReader();
            var runs = new List<ExamRun>();
            while (reader.Read())
                runs.Add(new ExamRun(reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2) != 0, ParseIso(reader.GetString(3)), ParseIso(reader.GetString(4)),
                    reader.GetInt32(5),
                    reader.GetString(6).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('/', 2)).Select(p => new ExamItemRef(p[0], p[1])).ToList(),
                    reader.IsDBNull(7) ? null : ParseIso(reader.GetString(7))));
            return runs;
        }
    }

    /// <summary>The attempts made inside one run, in submission order.</summary>
    public IReadOnlyList<Attempt> ExamAttempts(long runId) => ReadAttempts("WHERE exam_run_id = $run", ("$run", runId));

    // ----- reads ------------------------------------------------------------------------------

    /// <summary>All regular attempts in submission order, optionally for one exercise. Exam attempts are excluded; see <see cref="ExamAttempts"/>. Includes sources and TRX; cheap at this scale.</summary>
    public IReadOnlyList<Attempt> Attempts(string? exerciseId = null) =>
        ReadAttempts("WHERE exam_run_id IS NULL AND ($exercise IS NULL OR exercise_id = $exercise)", ("$exercise", exerciseId));

    private List<Attempt> ReadAttempts(string where, params (string Name, object? Value)[] parameters)
    {
        lock (_lock)
        {
            using var reader = Execute(
                $"SELECT id, exercise_id, item_id, timestamp, time_spent_ms, passed, answer, source, grading_json, trx, exam_run_id FROM attempt {where} ORDER BY id",
                parameters).ExecuteReader();
            var attempts = new List<Attempt>();
            while (reader.Read())
                attempts.Add(new Attempt(
                    reader.GetString(1), reader.GetString(2), ParseIso(reader.GetString(3)), TimeSpan.FromMilliseconds(reader.GetInt64(4)),
                    reader.GetInt64(5) != 0,
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : (byte[])reader.GetValue(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9)) { Id = reader.GetInt64(0), ExamRunId = reader.IsDBNull(10) ? null : reader.GetInt64(10) });
            return attempts;
        }
    }

    public IReadOnlyList<SkillTransition> SkillLedger()
    {
        lock (_lock)
        {
            using var reader = Execute("SELECT skill_id, state, timestamp, cause FROM skill_transition ORDER BY id").ExecuteReader();
            var ledger = new List<SkillTransition>();
            while (reader.Read())
                ledger.Add(new SkillTransition(reader.GetString(0), (SkillState)reader.GetInt32(1), ParseIso(reader.GetString(2)), reader.GetString(3)));
            return ledger;
        }
    }

    public IReadOnlyDictionary<string, SkillStatus> HeldSkills() => ProgressDerivation.HeldSkills(SkillLedger());

    public IReadOnlySet<string> PassedItems(string exerciseId) => ProgressDerivation.PassedItems(Attempts(exerciseId), exerciseId);

    /// <summary>Lock state and progress of every module and exercise of the loaded course.</summary>
    public CourseProgress Query(Course course)
    {
        lock (_lock)
        {
            var attempts = Attempts();
            var held = HeldSkills();
            var modules = course.Modules.Select(m => new ModuleProgress(m, m.Exercises.Select(e =>
            {
                var passed = ProgressDerivation.PassedItems(attempts, e.Manifest.Id);
                return new ExerciseProgress(e, passed,
                    e.Manifest.Items.Count > 0 && e.Manifest.Items.All(i => passed.Contains(i.Id)),
                    ProgressDerivation.MissingSkills(e.Manifest.Requires, held));
            }).ToList())).ToList();
            return new CourseProgress(modules, held);
        }
    }

    private static string Iso(DateTimeOffset t) => t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseIso(string s) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
