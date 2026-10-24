// Client for SeeSharp's local plugin API (docs/IDE-PLUGINS.md). No IntelliJ classes, so it is testable without the IDE.
package dev.seesharp.rider

import com.google.gson.Gson
import com.google.gson.JsonParseException
import java.io.IOException
import java.net.URI
import java.net.URLEncoder
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.nio.file.Files
import java.nio.file.Path
import java.time.Duration

data class Server(val url: String, val token: String, val pid: Int)

data class MissingSkill(val id: String, val label: String, val level: Int?)
data class Items(val total: Int, val passed: Int, val coding: Boolean)
data class Exercise(
    val id: String, val title: String, val optional: Boolean, val unlocked: Boolean, val passed: Boolean,
    val missingSkills: List<MissingSkill>?, val items: Items,
    val starterDirectory: String?, val workspaceDirectory: String?,
    val openFile: String?, val openLine: Int?, val instructionsPath: String?,
)
data class Module(val id: String, val level: Int, val title: String, val unlocked: Boolean, val passed: Boolean, val exercises: List<Exercise>)
data class Course(val locale: String, val modules: List<Module>)
data class ExerciseDetail(val exercise: Exercise, val instructionsHtml: String?, val instructionsFellBack: Boolean)
data class TestResult(val name: String, val outcome: String, val duration: String?, val message: String?, val stackTrace: String?)
data class GradingResult(val outcome: String, val duration: String, val tests: List<TestResult>?, val buildErrors: List<String>?, val output: String?)
data class Grade(val passed: Boolean, val result: GradingResult)
data class WorkspaceFiles(val directory: String, val files: List<String>)

/** HTTP status 0 = not reachable. The message is the server's `error` (in SeeSharp's locale) or `HTTP <status>`. */
class ApiException(val status: Int, message: String) : Exception(message)

private val gson = Gson()

/** Where .NET's LocalApplicationData points on Windows; SEESHARP_SERVER_JSON overrides (tests, runIde against a scratch host). */
fun serverFile(): Path = System.getenv("SEESHARP_SERVER_JSON")?.let(Path::of)
    ?: Path.of(System.getenv("LOCALAPPDATA") ?: (System.getProperty("user.home") + "/AppData/Local"), "SeeSharp", "server.json")

fun readServer(file: Path = serverFile()): Server? = try {
    gson.fromJson(Files.readString(file), Server::class.java)?.takeIf { (it.url as String?) != null && (it.token as String?) != null } // Gson ignores Kotlin nullability
} catch (e: IOException) {
    null
} catch (e: JsonParseException) {
    null
}

class Api(private val server: Server) {
    // HTTP/1.1: no h2c upgrade dance with Kestrel. java.net.http sends no Origin header, which the server would reject.
    private val client = HttpClient.newBuilder().version(HttpClient.Version.HTTP_1_1).connectTimeout(Duration.ofSeconds(3)).build()

    fun course(): Course = call("GET", "/api/course")
    fun exercise(id: String): ExerciseDetail = call("GET", "/api/exercise/${encodePath(id)}")
    fun grade(id: String): Grade = call("POST", "/api/exercise/${encodePath(id)}/grade")
    fun workspace(id: String): WorkspaceFiles = call("POST", "/api/exercise/${encodePath(id)}/workspace")

    private inline fun <reified T> call(method: String, route: String): T {
        val request = HttpRequest.newBuilder(URI.create(server.url.trimEnd('/') + route))
            .header("X-SeeSharp-Token", server.token)
            .timeout(Duration.ofMinutes(5)) // a grade builds and tests; GradingRunner's own timeout is 120 s
            .method(method, HttpRequest.BodyPublishers.noBody())
            .build()
        val response = try {
            client.send(request, HttpResponse.BodyHandlers.ofString())
        } catch (e: IOException) {
            throw ApiException(0, e.message ?: e.javaClass.simpleName)
        }
        val status = response.statusCode()
        val body = response.body()
        if (status !in 200..299) {
            val error = try { gson.fromJson(body, ErrorBody::class.java)?.error } catch (e: JsonParseException) { null }
            throw ApiException(status, error ?: "HTTP $status")
        }
        return parse(body)
    }

    private class ErrorBody(val error: String?)
}

/** JSON → model; public for the mapping tests. */
inline fun <reified T> parse(json: String): T = try {
    Gson().fromJson(json, T::class.java) ?: throw ApiException(0, "empty response")
} catch (e: JsonParseException) {
    throw ApiException(0, "invalid response: ${e.message}")
}
