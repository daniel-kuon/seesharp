package dev.seesharp.rider

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Assumptions.assumeTrue
import org.junit.jupiter.api.Test
import java.nio.file.Files
import java.nio.file.Path
import java.util.Properties

/** JSON mapping, strings and HTML always; the API part needs -PserverJson=<server.json of a scratch SeeSharp host on a course copy>. */
class ApiTest {
    @Test
    fun `course JSON maps, nulls included`() {
        val course = parse<Course>("""{"locale":"de","modules":[{"id":"m0","level":0,"title":"Werkzeuge","unlocked":true,"passed":false,
            "exercises":[{"id":"e1","title":"Hallo","optional":true,"unlocked":false,"passed":false,
              "missingSkills":[{"id":"s","label":"Schleifen","level":3},{"id":"t","label":"Typen","level":null}],
              "items":{"total":2,"passed":1,"coding":true},"starterDirectory":null,"workspaceDirectory":"D:\\Kurs, Ü\\starter",
              "openFile":null,"openLine":7,"instructionsPath":"x.md"}]}]}""")
        val e = course.modules.single().exercises.single()
        assertEquals("de", course.locale)
        assertTrue(e.optional)
        assertEquals(3, e.missingSkills!![0].level)
        assertNull(e.missingSkills[1].level)
        assertEquals(Items(2, 1, true), e.items)
        assertEquals("D:\\Kurs, Ü\\starter", e.workspaceDirectory)
        assertNull(e.openFile)
        assertEquals(7, e.openLine)
    }

    @Test
    fun `grade JSON maps`() {
        val g = parse<Grade>("""{"passed":false,"result":{"outcome":"Failed","duration":"00:00:03.1234567",
            "tests":[{"name":"A","outcome":"Passed","duration":"00:00:00","message":null,"stackTrace":null},
                     {"name":"B<script>","outcome":"Failed","duration":"00:00:00","message":"Expected <1> & got '2'","stackTrace":"at X"}],
            "buildErrors":[],"output":"…","artifactsDirectory":"C:\\t"}}""")
        assertFalse(g.passed)
        assertEquals(2, g.result.tests!!.size)
        assertEquals("Expected <1> & got '2'", g.result.tests[1].message)
    }

    @Test
    fun `invalid JSON is an ApiException`() {
        assertThrows(ApiException::class.java) { parse<Course>("<html>") }
    }

    @Test
    fun `results escape test names and messages`() {
        Strings.locale = "de"
        val g = Grade(false, GradingResult("Failed", "00:00:03.1234567", listOf(
            TestResult("A", "Passed", null, null, null),
            TestResult("B<script>alert(1)</script>", "Failed", null, "Expected <1> & got \"2\"", null)), emptyList(), ""))
        val html = resultsHtml(g)
        assertFalse(html.contains("<script>"))
        assertTrue(html.contains("B&lt;script&gt;alert(1)&lt;/script&gt;"))
        assertTrue(html.contains("Expected &lt;1&gt; &amp; got &quot;2&quot;"))
        assertTrue(html.contains("Noch nicht bestanden") && html.contains("00:00:03<"))
        assertEquals("1 von 2 Tests bestanden.", gradeSummary(g.result))
        assertTrue(resultsHtml(g.copy(result = g.result.copy(buildErrors = listOf("CS1002: ; erwartet <x>")))).contains("CS1002: ; erwartet &lt;x&gt;"))
    }

    @Test
    fun `locked exercise shows no instructions, only missing skills`() {
        Strings.locale = "de"
        val ex = Exercise("e", "Titel <b>", false, false, false, listOf(MissingSkill("s", "Schleifen", 3)), Items(1, 0, true),
            null, null, null, null, null)
        val html = exerciseHtml(ExerciseDetail(ex, "<h1>geheim</h1>", false), null, "http://127.0.0.1:1", "")
        assertFalse(html.contains("geheim"))
        assertTrue(html.contains("Schleifen (Level 3)") && html.contains("Titel &lt;b&gt;"))
        assertTrue(html.contains("default-src 'none'"))
    }

    @Test
    fun `strings - German by default, English fallback, same keys`() {
        fun keys(name: String) = Properties().apply { ApiTest::class.java.getResourceAsStream("/messages/$name")!!.reader(Charsets.UTF_8).use(::load) }.keys
        assertEquals(keys("SeeSharp.properties"), keys("SeeSharp_de.properties"))
        Strings.locale = "de"
        assertEquals("SeeSharp läuft nicht.", t("notRunning"))
        assertEquals("Den Projektordner von „Hallo“ in Rider öffnen?", t("openProject", "Hallo"))
        Strings.locale = "en"
        assertEquals("SeeSharp is not running.", t("notRunning"))
        Strings.locale = "fr"
        assertEquals("Check", t("check"))
        Strings.locale = "de"
    }

    @Test
    fun `missing or broken server json means not running`() {
        assertNull(readServer(Path.of("does-not-exist", "server.json")))
        val f = Files.createTempFile("server", ".json")
        Files.writeString(f, "{ broken")
        assertNull(readServer(f))
        Files.writeString(f, """{"pid":1}""")
        assertNull(readServer(f))
        Files.delete(f)
    }

    @Test
    fun `unreachable server is status 0`() {
        val e = assertThrows(ApiException::class.java) { Api(Server("http://127.0.0.1:9", "x", 1)).course() }
        assertEquals(0, e.status)
    }

    @Test
    fun `API against a running SeeSharp`() {
        val file = System.getProperty("seesharp.serverJson")
        assumeTrue(file != null, "set -PserverJson")
        val server = readServer(Path.of(file))!!
        val api = Api(server)

        assertEquals(403, assertThrows(ApiException::class.java) { Api(server.copy(token = "nope")).course() }.status)

        val course = api.course()
        assertTrue(course.modules.isNotEmpty())
        val exercises = course.modules.flatMap { it.exercises }
        val coding = exercises.first { it.unlocked && it.items.coding }
        assertTrue(Path.of(coding.openFile!!).isAbsolute && Files.exists(Path.of(coding.openFile!!)))
        assertTrue(coding.openFile!!.startsWith(coding.workspaceDirectory!!))
        exercises.firstOrNull { !it.unlocked }?.let { assertTrue(it.missingSkills!!.isNotEmpty()) }

        val detail = api.exercise(coding.id)
        assertEquals(coding.id, detail.exercise.id)
        assertTrue(detail.instructionsHtml!!.contains("<h1"))
        val notFound = assertThrows(ApiException::class.java) { api.exercise("does-not-exist") }
        assertEquals(404, notFound.status)
        assertTrue(notFound.message!!.isNotBlank() && !notFound.message!!.startsWith("HTTP"), "the server's error text")
        exercises.firstOrNull { !it.unlocked && it.items.coding }?.let { locked ->
            assertNull(api.exercise(locked.id).instructionsHtml)
            assertEquals(409, assertThrows(ApiException::class.java) { api.grade(locked.id) }.status)
        }

        val ws = api.workspace(coding.id)
        assertTrue(ws.files.contains("AGENTS.md"))
        assertTrue(Files.isDirectory(Path.of(ws.directory)))

        val grade = api.grade(coding.id)
        assertTrue(grade.result.outcome in setOf("Passed", "Failed", "BuildFailed", "TimedOut"))
        println("graded ${coding.id}: passed=${grade.passed} ${grade.result.outcome}, ${grade.result.tests.orEmpty().size} tests")
    }
}
