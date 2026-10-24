import java.time.LocalDateTime
import java.time.format.DateTimeFormatter

plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "2.4.21"
    id("org.jetbrains.intellij.platform") version "2.19.0"     // https://github.com/JetBrains/intellij-platform-gradle-plugin/releases
    id("me.filippov.gradle.jvm.wrapper") version "0.15.0"      // JDK 21 for ./gradlew (system java may be older)
}

val ProductVersion: String by project
val SinceBuild: String by project
val pluginId = "dev.seesharp.rider"
val gson = "com.google.code.gson:gson:2.11.0" // the platform bundles Gson; tests run without the platform
val junit = "org.junit.jupiter:junit-jupiter:5.13.4"
val kotlinStdLib = "org.jetbrains.kotlin:kotlin-stdlib:2.4.21"

repositories {
    maven { setUrl("https://cache-redirector.jetbrains.com/maven-central") }
    intellijPlatform {
        defaultRepositories()
        jetbrainsRuntime()
    }
}

// publishLocal stamps a build number so Rider sees every local build as an update.
val isLocalPublish = gradle.startParameter.taskNames.any { it.substringAfterLast(':') == "publishLocal" }
version = (extra["PluginVersion"] as String).let { base ->
    if (!isLocalPublish) base
    else base.split('.').take(2).joinToString(".") + "." + LocalDateTime.now().format(DateTimeFormatter.ofPattern("yyMMdd.HHmm"))
}

dependencies {
    intellijPlatform {
        rider(ProductVersion) { useInstaller = false }
        jetbrainsRuntime()
        bundledPlugin("com.intellij.modules.jcef") // JBCefBrowser lives in this bundled plugin (Rider 2026.2)
    }
    testImplementation(kotlinStdLib)
    testImplementation(gson)
    testImplementation(junit)
}

// Unit tests run without the IntelliJ Platform on the classpath: the API client and the JSON mapping need none.
val unitTestRuntime: Configuration by configurations.creating
dependencies {
    unitTestRuntime(kotlinStdLib)
    unitTestRuntime(gson)
    unitTestRuntime(junit)
    unitTestRuntime("org.junit.platform:junit-platform-launcher:1.13.4")
}

// ./gradlew unitTest [-PserverJson=<server.json of a scratch SeeSharp host on a course copy>]
val unitTest by tasks.registering(Test::class) {
    description = "API client and JSON mapping tests (no IDE)"
    group = "verification"
    useJUnitPlatform()
    testClassesDirs = sourceSets.test.get().output.classesDirs
    classpath = sourceSets.test.get().output + sourceSets.main.get().output + unitTestRuntime
    providers.gradleProperty("serverJson").orNull?.let { systemProperty("seesharp.serverJson", it) }
    testLogging { events("passed", "failed", "skipped"); showStandardStreams = true }
}

tasks.test {
    enabled = false // the IntelliJ test harness is not needed; see unitTest
    dependsOn(unitTest)
}

tasks.patchPluginXml {
    sinceBuild.set(SinceBuild)
    untilBuild.set(provider { null })
}

tasks.runIde {
    maxHeapSize = "1500m"
    // ./gradlew runIde -PserverJson=<server.json of a scratch host> : talk to that host instead of the user's SeeSharp
    providers.gradleProperty("serverJson").orNull?.let { environment("SEESHARP_SERVER_JSON", it) }
}

// Local plugin repository shared with other plugins (Settings › Plugins › Manage Plugin Repositories… has its URL).
// ./gradlew publishLocal [-PlocalRepoDir=<dir>]
val publishLocal by tasks.registering {
    group = "publishing"
    description = "Publish the plugin zip to the local custom plugin repository (updatePlugins.xml)"
    dependsOn(tasks.buildPlugin)
    val repoDir = providers.gradleProperty("localRepoDir")
        .orElse(System.getProperty("user.home") + "/.lamarider/plugin-repo")
    val zip = tasks.buildPlugin.flatMap { it.archiveFile }
    val pluginVersion = version.toString()
    val sinceBuild = SinceBuild
    doLast {
        val dir = file(repoDir.get()).apply { mkdirs() }
        val target = File(dir, zip.get().asFile.name)
        zip.get().asFile.copyTo(target, overwrite = true)
        // Keep the three newest SeeSharp builds; the other plugins' zips are not ours.
        dir.listFiles { f -> f.name.startsWith("SeeSharp-") && f.extension == "zip" }!!
            .sortedByDescending { it.lastModified() }.drop(3).forEach { it.delete() }
        val xml = File(dir, "updatePlugins.xml")
        val entry = """
            |  <plugin id="$pluginId" url="${target.toPath().toUri()}" version="$pluginVersion">
            |    <idea-version since-build="$sinceBuild"/>
            |    <name>SeeSharp</name>
            |    <vendor>SeeSharp</vendor>
            |    <description>SeeSharp C# training: exercises, instructions and grading in Rider.</description>
            |  </plugin>""".trimMargin()
        // Replace only our own <plugin> entry (or add it before </plugins>); every other byte stays as it is.
        val old = if (xml.isFile) xml.readText() else "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<plugins>\n</plugins>\n"
        val ours = Regex("""(?s)  <plugin id="${Regex.escape(pluginId)}".*?</plugin>""").find(old)
        val new = if (ours != null) old.replaceRange(ours.range, entry)
            else old.lastIndexOf("</plugins>").let { i -> old.substring(0, i) + entry + "\n" + old.substring(i) }
        xml.writeText(new)
        println("SeeSharp $pluginVersion published. Repository URL: ${xml.toPath().toUri()}")
    }
}
