// "Set up SeeSharp": SDK check, tool lookup and argument lists. No IntelliJ classes, so unitTest covers them;
// the flow (dialogs, console, background task) is in SeeSharpToolWindow.kt.
package dev.seesharp.rider

import java.io.File
import java.nio.file.Files
import java.nio.file.Path

object Setup {
    const val PACKAGE_ID = "SeeSharp.Cli"
    const val REQUIRED_SDK = 10
    const val SDK_DOWNLOAD = "https://dotnet.microsoft.com/download/dotnet/10.0"
    const val NUGET_ORG = "https://api.nuget.org/v3/index.json"
    private val windows = System.getProperty("os.name").startsWith("Windows")

    /** Major versions from `dotnet --list-sdks`, whose lines look like `10.0.100 [C:\Program Files\dotnet\sdk]`. */
    fun sdkMajors(listSdks: String): List<Int> =
        listSdks.lines().mapNotNull { Regex("""^\s*(\d+)\.\d+\.\d+""").find(it)?.groupValues?.get(1)?.toInt() }

    fun hasSdk(listSdks: String, major: Int = REQUIRED_SDK) = major in sdkMajors(listSdks)

    /** `seesharp` on PATH, else in `~/.dotnet/tools` (where `dotnet tool install -g` puts it; Rider's PATH may predate that). */
    fun findTool(
        path: String? = System.getenv("PATH"), home: String = System.getProperty("user.home"), windows: Boolean = this.windows,
        exists: (Path) -> Boolean = Files::isRegularFile,
    ): Path? {
        val name = if (windows) "seesharp.exe" else "seesharp"
        val separator = if (windows) ';' else ':'
        val dirs = path.orEmpty().split(separator).filter { it.isNotBlank() } + Path.of(home, ".dotnet", "tools").toString()
        return dirs.asSequence().mapNotNull { runCatching { Path.of(it.trim(), name) }.getOrNull() }.firstOrNull(exists)
    }

    /** `dotnet tool install|update -g SeeSharp.Cli --add-source <nuget.org> [--add-source <user's extra source>]`: sources named explicitly. */
    fun toolArgs(update: Boolean, extraSource: String?): List<String> =
        listOf("tool", if (update) "update" else "install", "-g", PACKAGE_ID, "--add-source", NUGET_ORG) +
            (extraSource?.trim()?.takeIf { it.isNotEmpty() }?.let { listOf("--add-source", it) } ?: emptyList())

    /** Every setup process runs here (the user's home), never in the project folder. */
    fun workDirectory(): String = System.getProperty("user.home")

    fun initArgs(courseDir: String) = listOf("init", "--dir", courseDir)
    fun startArgs() = listOf("start", "--no-browser")
    fun defaultCourseDir(home: String = System.getProperty("user.home")): String = home + File.separator + "seesharp-course"
}
