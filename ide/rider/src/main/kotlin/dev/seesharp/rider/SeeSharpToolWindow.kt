package dev.seesharp.rider

import com.intellij.execution.ExecutionException
import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.execution.process.OSProcessHandler
import com.intellij.execution.process.ProcessEvent
import com.intellij.execution.process.ProcessListener
import com.intellij.icons.AllIcons
import com.intellij.ide.BrowserUtil
import com.intellij.ide.impl.ProjectUtil
import com.intellij.ide.util.PropertiesComponent
import com.intellij.notification.NotificationAction
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.Disposable
import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.DefaultActionGroup
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.runReadActionBlocking
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.options.BoundConfigurable
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.DumbAwareAction
import com.intellij.openapi.project.Project
import com.intellij.openapi.roots.ProjectFileIndex
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.ui.SimpleToolWindowPanel
import com.intellij.openapi.util.Disposer
import com.intellij.openapi.util.Key
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowFactory
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.ui.ColorUtil
import com.intellij.ui.ColoredTreeCellRenderer
import com.intellij.ui.OnePixelSplitter
import com.intellij.ui.SimpleTextAttributes
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.content.ContentFactory
import com.intellij.ui.dsl.builder.AlignX
import com.intellij.ui.dsl.builder.bindText
import com.intellij.ui.dsl.builder.panel
import com.intellij.ui.jcef.JBCefApp
import com.intellij.ui.jcef.JBCefBrowser
import com.intellij.ui.scale.JBUIScale
import com.intellij.ui.treeStructure.Tree
import com.intellij.util.ui.JBUI
import com.intellij.util.ui.UIUtil
import org.cef.browser.CefBrowser
import org.cef.browser.CefFrame
import org.cef.handler.CefRequestHandlerAdapter
import org.cef.network.CefRequest
import java.awt.BorderLayout
import java.nio.file.Files
import java.nio.file.Path
import javax.swing.JButton
import javax.swing.JEditorPane
import javax.swing.JPanel
import javax.swing.JTree
import javax.swing.ToolTipManager
import javax.swing.event.HyperlinkEvent
import javax.swing.tree.DefaultMutableTreeNode
import javax.swing.tree.DefaultTreeModel
import javax.swing.tree.TreePath
import kotlin.math.roundToInt

private const val TOOL_WINDOW = "SeeSharp"
/** Exercise to continue with in the project that opening its folder brings up (application-wide). */
private const val PENDING = "seesharp.pendingExercise"
private const val CLI_PATH = "seesharp.cliPath"

class SeeSharpToolWindowFactory : ToolWindowFactory, DumbAware {
    override fun createToolWindowContent(project: Project, toolWindow: ToolWindow) {
        val panel = SeeSharpPanel(project)
        val content = ContentFactory.getInstance().createContent(panel, null, false)
        content.setDisposer(panel)
        toolWindow.contentManager.addContent(content)
    }
}

class SeeSharpConfigurable : BoundConfigurable("SeeSharp") {
    override fun createPanel() = panel {
        row(t("cliPath")) {
            textField()
                .bindText({ PropertiesComponent.getInstance().getValue(CLI_PATH).orEmpty() },
                    { PropertiesComponent.getInstance().setValue(CLI_PATH, it.trim(), "") })
                .align(AlignX.FILL)
                .comment(t("cliPathComment"))
        }
    }
}

/** What one GET /api/course gave: a course, or an API error to show (exam running, no course), or neither (not running). */
private class Snapshot(val api: Api?, val url: String, val course: Course?, val message: String?)

private fun fetch(): Snapshot {
    val server = readServer() ?: return Snapshot(null, "", null, null)
    val api = Api(server)
    return try {
        Snapshot(api, server.url, api.course(), null)
    } catch (e: ApiException) {
        if (e.status == 0) Snapshot(null, "", null, null) else Snapshot(api, server.url, null, e.message)
    }
}

private fun samePath(a: String, b: String) = Path.of(a).normalize().toString().equals(Path.of(b).normalize().toString(), ignoreCase = true)
private fun isUnder(file: String, dir: String) = Path.of(file).normalize().toString().lowercase().startsWith(Path.of(dir).normalize().toString().lowercase() + java.io.File.separator)

class SeeSharpPanel(private val project: Project) : SimpleToolWindowPanel(true, true), Disposable {
    // EDT state
    private var api: Api? = null
    private var serverUrl = ""
    private var course: Course? = null
    private var detail: ExerciseDetail? = null
    private var lastGrade: Grade? = null
    private var running = false
    private var rebuilding = false
    private var shownHtml: String? = null

    private val tree = Tree(DefaultTreeModel(DefaultMutableTreeNode()))
    private val title = JBLabel()
    private val checkButton = JButton()
    private val browser = if (JBCefApp.isSupported()) JBCefBrowser() else null
    private val pane = JEditorPane("text/html", "")

    init {
        tree.isRootVisible = false
        tree.showsRootHandles = true
        tree.cellRenderer = Renderer()
        ToolTipManager.sharedInstance().registerComponent(tree)
        tree.addTreeSelectionListener {
            if (!rebuilding) ((tree.lastSelectedPathComponent as? DefaultMutableTreeNode)?.userObject as? Exercise)?.let { openExercise(it.id, true) }
        }

        val toolbar = ActionManager.getInstance().createActionToolbar("SeeSharp", DefaultActionGroup(RefreshAction(), StartAction()), true)
        toolbar.targetComponent = this
        setToolbar(toolbar.component)

        checkButton.addActionListener { grade() }
        val header = JPanel(BorderLayout()).apply {
            border = JBUI.Borders.empty(4, 8)
            add(title, BorderLayout.CENTER)
            add(checkButton, BorderLayout.EAST)
        }
        val view = if (browser != null) {
            Disposer.register(this, browser)
            // Links (the quiz-only exercise's "im Browser öffnen") open in the system browser, not in the panel.
            browser.jbCefClient.addRequestHandler(object : CefRequestHandlerAdapter() {
                override fun onBeforeBrowse(b: CefBrowser?, frame: CefFrame?, request: CefRequest?, userGesture: Boolean, isRedirect: Boolean): Boolean {
                    if (!userGesture) return false
                    request?.url?.let { BrowserUtil.browse(it) }
                    return true
                }
            }, browser.cefBrowser)
            browser.component
        } else {
            pane.isEditable = false
            pane.putClientProperty(JEditorPane.HONOR_DISPLAY_PROPERTIES, true)
            pane.addHyperlinkListener { if (it.eventType == HyperlinkEvent.EventType.ACTIVATED) it.url?.let(BrowserUtil::browse) }
            JBScrollPane(pane)
        }
        val details = JPanel(BorderLayout()).apply {
            add(header, BorderLayout.NORTH)
            add(view, BorderLayout.CENTER)
        }
        setContent(OnePixelSplitter(true, "SeeSharp.splitter", 0.4f).apply {
            firstComponent = JBScrollPane(tree)
            secondComponent = details
        })
        render()
        load(firstLoad = true)
    }

    override fun dispose() {}

    private fun background(f: () -> Unit) = ApplicationManager.getApplication().executeOnPooledThread(f)
    private fun ui(f: () -> Unit) = ApplicationManager.getApplication().invokeLater { if (!project.isDisposed) f() }

    private fun load(firstLoad: Boolean = false) {
        background { fetch().let { ui { apply(it, firstLoad) } } }
    }

    private fun apply(s: Snapshot, firstLoad: Boolean = false) {
        api = s.api
        serverUrl = s.url
        course = s.course
        s.course?.let { Strings.locale = it.locale }
        rebuildTree(s.message)
        render()
        if (s.course != null) resume(firstLoad)
    }

    /** In the project that opening an exercise's folder brought up: continue with it; else the exercise this project is. */
    private fun resume(firstLoad: Boolean) {
        val exercises = course?.modules.orEmpty().flatMap { it.exercises }
        val base = project.basePath ?: return
        val pending = PropertiesComponent.getInstance().getValue(PENDING)
        exercises.firstOrNull { it.id == pending && it.openFile != null && isUnder(it.openFile, base) }?.let {
            PropertiesComponent.getInstance().unsetValue(PENDING)
            select(it.id, trigger = true)
            return
        }
        if (firstLoad && detail == null)
            exercises.firstOrNull { it.unlocked && it.items.coding && it.workspaceDirectory != null && samePath(it.workspaceDirectory, base) }?.let {
                select(it.id, trigger = false)
                openExercise(it.id, false)
            }
    }

    private fun rebuildTree(message: String?) {
        val root = DefaultMutableTreeNode()
        for (m in course?.modules.orEmpty())
            root.add(DefaultMutableTreeNode(m).apply { m.exercises.forEach { add(DefaultMutableTreeNode(it, false)) } })
        rebuilding = true
        try {
            (tree.model as DefaultTreeModel).setRoot(root)
            val next = root.children().asSequence().map { it as DefaultMutableTreeNode }.firstOrNull { (it.userObject as Module).let { m -> m.unlocked && !m.passed } }
            next?.let { tree.expandPath(TreePath(it.path)) }
            detail?.let { select(it.exercise.id, trigger = false) }
        } finally {
            rebuilding = false
        }
        tree.emptyText.clear()
        when {
            course != null -> {}
            message != null -> tree.emptyText.text = message // exam running, no course loaded, ...
            else -> {
                tree.emptyText.text = t("notRunning")
                tree.emptyText.appendLine(t("start"), SimpleTextAttributes.LINK_PLAIN_ATTRIBUTES) { start() }
                tree.emptyText.appendLine(t("refresh"), SimpleTextAttributes.LINK_PLAIN_ATTRIBUTES) { load() }
            }
        }
    }

    private fun select(id: String, trigger: Boolean) {
        val root = tree.model.root as DefaultMutableTreeNode
        val node = root.depthFirstEnumeration().asSequence().map { it as DefaultMutableTreeNode }.firstOrNull { (it.userObject as? Exercise)?.id == id } ?: return
        val wasRebuilding = rebuilding
        rebuilding = !trigger
        try {
            TreePath(node.path).let { tree.expandPath(it.parentPath); tree.selectionPath = it; tree.scrollPathToVisible(it) }
        } finally {
            rebuilding = wasRebuilding
        }
    }

    private fun openExercise(id: String, navigate: Boolean) {
        val client = api ?: return offline()
        background {
            try {
                val d = client.exercise(id)
                val ex = d.exercise
                var file: VirtualFile? = null
                var inProject = false
                if (navigate && ex.unlocked && ex.items.coding && ex.openFile != null) {
                    client.workspace(id)
                    file = LocalFileSystem.getInstance().refreshAndFindFileByNioFile(Path.of(ex.openFile))
                    val base = project.basePath
                    inProject = (base != null && isUnder(ex.openFile, base))
                        || (file != null && runReadActionBlocking { !project.isDisposed && ProjectFileIndex.getInstance(project).isInContent(file) })
                }
                ui {
                    detail = d
                    lastGrade = null
                    render()
                    if (navigate && ex.unlocked && ex.items.coding) navigate(ex, file, inProject)
                }
            } catch (e: ApiException) {
                ui { report(e) }
            }
        }
    }

    private fun navigate(ex: Exercise, file: VirtualFile?, inProject: Boolean) {
        if (!inProject) {
            val choice = Messages.showDialog(project, t("openProject", ex.title), "SeeSharp",
                arrayOf(t("openHere"), t("openNewWindow"), t("notNow")), 0, Messages.getQuestionIcon())
            if (choice == 0 || choice == 1) {
                // Rider opens the starter's .csproj (like `seesharp` does), else the folder. The new project resumes in resume().
                val dir = Path.of(ex.workspaceDirectory ?: Path.of(ex.openFile!!).parent.toString())
                val target = Files.list(dir).use { s -> s.filter { it.fileName.toString().endsWith(".csproj", true) }.toList() }.singleOrNull() ?: dir
                PropertiesComponent.getInstance().setValue(PENDING, ex.id)
                ProjectUtil.openOrImport(target, project, choice == 1)?.let { p ->
                    ToolWindowManager.getInstance(p).invokeLater { ToolWindowManager.getInstance(p).getToolWindow(TOOL_WINDOW)?.activate(null) }
                }
                return
            }
        }
        file?.let { OpenFileDescriptor(project, it, maxOf(0, (ex.openLine ?: 1) - 1), 0).navigate(true) }
    }

    private fun grade() {
        val client = api ?: return offline()
        val ex = detail?.exercise ?: return
        FileDocumentManager.getInstance().saveAllDocuments() // grading copies the files from disk
        running = true
        render()
        object : Task.Backgroundable(project, t("runningTitle", ex.title), false) {
            private var result: Grade? = null
            override fun run(indicator: ProgressIndicator) {
                result = client.grade(ex.id)
            }

            override fun onSuccess() {
                val g = result ?: return
                if (detail?.exercise?.id == ex.id) lastGrade = g
                notify(t(if (g.passed) "passedSummary" else "notPassedSummary", gradeSummary(g.result)),
                    if (g.passed) NotificationType.INFORMATION else NotificationType.WARNING)
                load()
            }

            override fun onThrowable(error: Throwable) {
                if (error is ApiException) report(error) else super.onThrowable(error)
            }

            override fun onFinished() {
                running = false
                render()
            }
        }.queue()
    }

    /** `seesharp start --no-browser`, or `dotnet run` on a checkout; a plain process, no shell, so paths need no quoting. */
    private fun start() {
        val cli = PropertiesComponent.getInstance().getValue(CLI_PATH)?.trim().orEmpty()
        val command = if (cli.isEmpty()) {
            // Installed after Rider started: ~/.dotnet/tools is not on Rider's PATH yet.
            val tool = Path.of(System.getProperty("user.home"), ".dotnet", "tools", "seesharp.exe")
            GeneralCommandLine(if (Files.isRegularFile(tool)) tool.toString() else "seesharp", "start", "--no-browser")
        } else {
            val checkout = Path.of(cli, "src", "SeeSharp.Cli").takeIf { Files.isDirectory(it) } ?: Path.of(cli)
            GeneralCommandLine("dotnet", "run", "--project", checkout.toString(), "--", "start", "--no-browser")
        }.withParentEnvironmentType(GeneralCommandLine.ParentEnvironmentType.CONSOLE).withWorkDirectory(System.getProperty("user.home"))

        object : Task.Backgroundable(project, t("starting"), true) {
            override fun run(indicator: ProgressIndicator) {
                val output = StringBuffer()
                val handler = try {
                    OSProcessHandler(command)
                } catch (e: ExecutionException) {
                    return failed(e.message.orEmpty())
                }
                handler.addProcessListener(object : ProcessListener {
                    override fun onTextAvailable(event: ProcessEvent, outputType: Key<*>) {
                        if (output.length < 20_000) output.append(event.text)
                    }
                })
                handler.startNotify()
                repeat(120) { // dotnet run builds first
                    indicator.checkCanceled()
                    Thread.sleep(1000)
                    val s = fetch()
                    if (s.api != null) return ui { apply(s) }
                    if (handler.isProcessTerminated) return failed(output.toString().takeLast(1500))
                }
                failed(output.toString().takeLast(1500))
            }

            private fun failed(details: String) = ui { notify(t("startFailed", details), NotificationType.ERROR) }
        }.queue()
    }

    private fun offline() {
        notify(t("notRunning"), NotificationType.WARNING, NotificationAction.createSimpleExpiring(t("start")) { start() })
        load()
    }

    private fun report(e: ApiException) = if (e.status == 0) offline() else notify(e.message.orEmpty(), NotificationType.ERROR)

    private fun notify(text: String, type: NotificationType, vararg actions: NotificationAction) =
        NotificationGroupManager.getInstance().getNotificationGroup("SeeSharp").createNotification("SeeSharp", escapeHtml(text), type)
            .apply { actions.forEach { addAction(it) } }.notify(project)

    private fun render() {
        val d = detail
        val ex = d?.exercise
        title.text = ex?.title ?: t("selectExercise")
        checkButton.text = t(if (running) "testsRunning" else "check")
        checkButton.isVisible = ex != null && ex.unlocked && ex.items.coding
        checkButton.isEnabled = !running && api != null
        val html = if (d == null) "" else exerciseHtml(d, lastGrade, serverUrl, css())
        if (html == shownHtml) return
        shownHtml = html
        if (browser != null) browser.loadHTML(html)
        else {
            pane.text = html
            pane.caretPosition = 0
        }
    }

    private fun css(): String {
        val font = UIUtil.getLabelFont()
        // JCEF scales CSS pixels itself; Swing's HTML does not.
        val size = if (browser != null) (font.size2D / JBUIScale.scale(1f)).roundToInt() else font.size
        val fg = ColorUtil.toHtmlColor(UIUtil.getLabelForeground())
        val bg = ColorUtil.toHtmlColor(UIUtil.getPanelBackground())
        val link = ColorUtil.toHtmlColor(JBUI.CurrentTheme.Link.Foreground.ENABLED)
        return """
            body { font-family: '${font.family}', sans-serif; font-size: ${size}px; color: $fg; background: $bg; line-height: 1.5; margin: 8px; }
            a { color: $link; } h1 { font-size: 1.4em; } .ok { color: #5fb865; } .bad { color: #e55765; } .note { color: #888888; }
            pre { background: rgba(127,127,127,.15); padding: 6px; overflow-x: auto; } table { border-collapse: collapse; }
            td { padding: 2px 8px; vertical-align: top; border-bottom: 1px solid rgba(127,127,127,.3); }
        """.trimIndent()
    }

    private inner class Renderer : ColoredTreeCellRenderer() {
        override fun customizeCellRenderer(tree: JTree, value: Any?, selected: Boolean, expanded: Boolean, leaf: Boolean, row: Int, hasFocus: Boolean) {
            when (val o = (value as? DefaultMutableTreeNode)?.userObject) {
                is Module -> {
                    icon = if (o.passed) AllIcons.RunConfigurations.TestPassed else if (o.unlocked) AllIcons.Nodes.Folder else AllIcons.Nodes.Locked
                    append("%02d %s".format(o.level, o.title))
                    toolTipText = null
                }
                is Exercise -> {
                    icon = if (!o.unlocked) AllIcons.Nodes.Locked else if (o.passed) AllIcons.RunConfigurations.TestPassed else AllIcons.RunConfigurations.TestNotRan
                    append(o.title)
                    if (o.optional) append("  " + t("optional"), SimpleTextAttributes.GRAYED_ATTRIBUTES)
                    toolTipText = buildString {
                        append("<html><b>").append(escapeHtml(o.title)).append("</b><br>").append(escapeHtml(t("tasksDone", o.items.passed, o.items.total)))
                        if (!o.unlocked) {
                            append("<br>").append(escapeHtml(t("missingSkills")))
                            o.missingSkills.orEmpty().forEach { append("<br>• ").append(escapeHtml(skillText(it))) }
                        }
                        append("</html>")
                    }
                }
            }
        }
    }

    private inner class RefreshAction : DumbAwareAction(AllIcons.Actions.Refresh) {
        override fun getActionUpdateThread() = ActionUpdateThread.EDT
        override fun update(e: AnActionEvent) {
            e.presentation.text = t("refresh")
        }
        override fun actionPerformed(e: AnActionEvent) = load()
    }

    private inner class StartAction : DumbAwareAction(AllIcons.Actions.Execute) {
        override fun getActionUpdateThread() = ActionUpdateThread.EDT
        override fun update(e: AnActionEvent) {
            e.presentation.text = t("start")
            e.presentation.isEnabled = api == null
        }
        override fun actionPerformed(e: AnActionEvent) = start()
    }
}
