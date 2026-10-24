// UI strings and the exercise HTML. No IntelliJ classes, so both are testable without the IDE.
package dev.seesharp.rider

import java.text.MessageFormat
import java.util.Locale
import java.util.ResourceBundle

/** Strings follow SeeSharp's locale (from GET /api/course, German until known), like the course content; English is the fallback. */
object Strings {
    @Volatile var locale: String = "de"

    fun t(key: String, vararg args: Any?): String {
        val bundle = ResourceBundle.getBundle("messages.SeeSharp", Locale.forLanguageTag(locale), Strings::class.java.classLoader,
            ResourceBundle.Control.getNoFallbackControl(ResourceBundle.Control.FORMAT_PROPERTIES))
        return MessageFormat(bundle.getString(key), Locale.ROOT).format(args)
    }
}

fun t(key: String, vararg args: Any?) = Strings.t(key, *args)

fun escapeHtml(s: String): String = buildString(s.length) {
    for (c in s) when (c) {
        '&' -> append("&amp;"); '<' -> append("&lt;"); '>' -> append("&gt;"); '"' -> append("&quot;"); '\'' -> append("&#39;")
        else -> append(c)
    }
}

fun skillText(s: MissingSkill) = if (s.level == null) s.label else t("skillLevel", s.label, s.level)

fun outcomeText(outcome: String) = when (outcome) {
    "Passed", "Failed", "BuildFailed", "TimedOut" -> t("outcome.$outcome")
    else -> outcome
}

/** "2 von 2 Tests bestanden." or, without tests (build failed, timeout), the outcome. */
fun gradeSummary(r: GradingResult): String {
    val tests = r.tests.orEmpty()
    return if (tests.isEmpty()) outcomeText(r.outcome) else t("testsPassed", tests.count { it.outcome == "Passed" }, tests.size)
}

/**
 * The details view: last grade of this exercise, then the instructions. Course content (the trainee's own checkout) is rendered
 * as is; the CSP blocks scripts in it. Test names, messages and output come from the trainee's code and are escaped.
 */
fun exerciseHtml(d: ExerciseDetail, grade: Grade?, serverUrl: String, css: String): String {
    val ex = d.exercise
    val body = StringBuilder()
    if (!ex.unlocked) {
        body.append("<p>${escapeHtml(t("locked"))}</p><ul>")
        ex.missingSkills.orEmpty().forEach { body.append("<li>${escapeHtml(skillText(it))}</li>") }
        body.append("</ul>")
    } else {
        if (!ex.items.coding)
            body.append("<p>${escapeHtml(t("quizOnly"))} <a href=\"${escapeHtml("${serverUrl.trimEnd('/')}/exercise/${encodePath(ex.id)}")}\">${
                escapeHtml(t("openInBrowser"))}</a></p>")
        if (grade != null) body.append(resultsHtml(grade))
        body.append("<hr>")
        if (d.instructionsFellBack) body.append("<p class=\"note\">${escapeHtml(t("fellBack"))}</p>")
        body.append(d.instructionsHtml.orEmpty())
    }
    return """<!DOCTYPE html><html><head><meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src https: data:;">
<style>$css</style></head><body><h1>${escapeHtml(ex.title)}</h1>$body</body></html>"""
}

fun resultsHtml(g: Grade): String {
    val r = g.result
    val html = StringBuilder("<p class=\"${if (g.passed) "ok" else "bad"}\"><strong>${escapeHtml(t(if (g.passed) "passed" else "notPassed"))}</strong> · ${
        escapeHtml(outcomeText(r.outcome))} · ${escapeHtml(r.duration.substringBefore('.'))}</p>")
    val tests = r.tests.orEmpty()
    when {
        !r.buildErrors.isNullOrEmpty() -> html.append("<pre>${escapeHtml(r.buildErrors.joinToString("\n"))}</pre>")
        tests.isNotEmpty() -> {
            html.append("<table>")
            for (x in tests) {
                val (cls, mark) = when (x.outcome) { "Passed" -> "ok" to "✓"; "Failed" -> "bad" to "✗"; else -> "" to "–" }
                html.append("<tr><td class=\"$cls\">$mark</td><td><code>${escapeHtml(x.name)}</code>")
                if (!x.message.isNullOrEmpty()) html.append("<pre>${escapeHtml(x.message)}</pre>")
                html.append("</td></tr>")
            }
            html.append("</table>")
        }
        else -> html.append("<pre>${escapeHtml(r.output.orEmpty().takeLast(3000))}</pre>")
    }
    return html.toString()
}

internal fun encodePath(s: String) = java.net.URLEncoder.encode(s, Charsets.UTF_8).replace("+", "%20")
