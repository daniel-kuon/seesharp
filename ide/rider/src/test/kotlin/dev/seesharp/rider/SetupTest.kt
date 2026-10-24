package dev.seesharp.rider

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.nio.file.Path

class SetupTest {
    @Test
    fun `SDK detection reads the majors of dotnet --list-sdks`() {
        val out = "8.0.404 [C:\\Program Files\\dotnet\\sdk]\r\n10.0.100 [C:\\Program Files\\dotnet\\sdk]\r\n11.0.100-rc.2 [C:\\x]\r\n"
        assertEquals(listOf(8, 10, 11), Setup.sdkMajors(out))
        assertTrue(Setup.hasSdk(out))
        assertFalse(Setup.hasSdk("8.0.404 [/usr/share/dotnet/sdk]\n"))
        assertFalse(Setup.hasSdk(""))
    }

    @Test
    fun `the tool is found on PATH first, then in the dotnet tools folder`() {
        val home = System.getProperty("java.io.tmpdir") + "Jörg Müller"
        val tools = Path.of(home, ".dotnet", "tools", "seesharp.exe")
        val onPath = Path.of(home, "b, c", "seesharp.exe")
        assertEquals(onPath, Setup.findTool(Path.of(home, "a").toString() + ";" + onPath.parent, home, true) { it == onPath || it == tools })
        assertEquals(tools, Setup.findTool(Path.of(home, "a").toString(), home, true) { it == tools })
        assertEquals(Path.of(home, ".dotnet", "tools", "seesharp"), Setup.findTool(null, home, false) { it.fileName.toString() == "seesharp" })
        assertNull(Setup.findTool("", home, true) { false })
    }

    @Test
    fun `argument lists keep paths with spaces, umlauts and commas as one argument`() {
        val nuget = listOf("--add-source", "https://api.nuget.org/v3/index.json")
        assertEquals(listOf("tool", "install", "-g", "SeeSharp.Cli") + nuget, Setup.toolArgs(false, null))
        assertEquals(listOf("tool", "install", "-g", "SeeSharp.Cli") + nuget, Setup.toolArgs(false, "  "))
        assertEquals(listOf("tool", "update", "-g", "SeeSharp.Cli") + nuget + listOf("--add-source", "D:\\Rider Projects 2\\SeeSharp\\nupkg"),
            Setup.toolArgs(true, " D:\\Rider Projects 2\\SeeSharp\\nupkg "))
        assertEquals(listOf("init", "--dir", "C:\\Kurs Ü, 1"), Setup.initArgs("C:\\Kurs Ü, 1"))
        assertEquals(listOf("start", "--no-browser"), Setup.startArgs())
    }

    @Test
    fun `setup processes run in the home folder, not the project`() {
        assertEquals(System.getProperty("user.home"), Setup.workDirectory())
    }
}
