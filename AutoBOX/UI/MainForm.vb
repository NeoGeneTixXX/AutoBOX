Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Windows.Forms

''' <summary>
''' GUI-Schicht: Batch-Eingabe von AutoBOX. Statt eines einzelnen Datensatzes
''' wird eine Liste (DataGridView) mit beliebig vielen Zeilen gepflegt – jede
''' Zeile ist ein Blechkasten mit eigener Breite B, Tiefe T, Laenge L und der
''' Option "Mit Deckel?". Ueber "Alle erstellen" werden alle Zeilen nacheinander
''' abgearbeitet (einmalige Solid-Edge-Verbindung). Die Oberflaeche wird im Code
''' aufgebaut, damit das Projekt ohne Designer-/resx-Dateien kompilierbar bleibt.
''' Der Dialog kennt nur die GUI und ruft die Automatisierung auf – die
''' Geometrie steckt komplett in der Automatisierungs-/Geometrie-Schicht.
'''
''' Oben im Dialog wird die Standardvorlage (Solid-Edge-Kantteil *.psm) gewaehlt,
''' aus der alle Teile erzeugt werden. Die Auswahl gilt fuer den gesamten Batch
''' und wird fuer den naechsten Programmstart gespeichert. Ebenso der Schenkel S
''' (Laenge der beiden unteren Flanschlinien am Hauptteil).
''' </summary>
Public Class MainForm
    Inherits Form

    Private ReadOnly lblUeberschrift As New Label()

    Private ReadOnly lblVorlage As New Label()
    ''' <summary>Reine Anzeige des Vorlagenpfads – aendern nur ueber "Durchsuchen" / "Standard".</summary>
    Private WithEvents lblVorlagePfad As New Label()
    Private ReadOnly tipVorlagePfad As New ToolTip()
    Private WithEvents btnVorlageWaehlen As New Button()
    Private WithEvents btnVorlageStandard As New Button()

    Private ReadOnly lblSchenkel As New Label()
    Private ReadOnly txtSchenkel As New TextBox()

    ''' <summary>Gespeicherte Programmeinstellungen (zuletzt gewaehlte Vorlage und Schenkel).</summary>
    Private ReadOnly _einstellungen As Einstellungen = Einstellungen.Laden()

    Private ReadOnly gridDatensaetze As New DataGridView()
    Private ReadOnly colBreite As New DataGridViewTextBoxColumn()
    Private ReadOnly colTiefe As New DataGridViewTextBoxColumn()
    Private ReadOnly colLaenge As New DataGridViewTextBoxColumn()
    Private ReadOnly colDeckel As New DataGridViewCheckBoxColumn()
    Private ReadOnly colStatus As New DataGridViewTextBoxColumn()

    Private WithEvents btnZeileHinzufuegen As New Button()
    Private WithEvents btnZeileDuplizieren As New Button()
    Private WithEvents btnZeileLoeschen As New Button()
    Private WithEvents btnAlleErstellen As New Button()
    Private WithEvents btnBeenden As New Button()

    Private ReadOnly lblProtokoll As New Label()
    Private ReadOnly txtProtokoll As New TextBox()

    Public Sub New()
        InitialisiereOberflaeche()
        lblVorlagePfad.Text = _einstellungen.VorlagePfad   ' zuletzt gewaehlte Vorlage vorbelegen
        txtSchenkel.Text = _einstellungen.SchenkelMm.ToString(CultureInfo.CurrentCulture)
        ZeigeVorlagenHinweis()
        FuegeStandardzeile()   ' Startet mit einer vorbelegten Beispielzeile
    End Sub

    ''' <summary>Nach dem Anzeigen: Update-Pruefung im Hintergrund (blockiert die GUI nicht).</summary>
    Protected Overrides Async Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        Await UpdatePruefung.PruefeAsync(Me)
    End Sub

    Private Sub InitialisiereOberflaeche()
        Me.Text = "AutoBOX – Blechteil-Generator für Solid Edge (Stapelverarbeitung)"
        Me.ClientSize = New Size(680, 672)
        Me.MinimumSize = New Size(560, 552)
        Me.FormBorderStyle = FormBorderStyle.Sizable
        Me.MaximizeBox = True
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Font = New Font("Segoe UI", 9.0F)

        ' Fenstersymbol = eingebettetes EXE-Icon (kleine Kiste)
        Try
            Me.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
            ' Ohne Icon weiterarbeiten
        End Try

        lblUeberschrift.Text = "Blechkasten (Konturlappen) + optionaler Deckel – Liste abarbeiten"
        lblUeberschrift.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblUeberschrift.Location = New Point(20, 15)
        lblUeberschrift.AutoSize = True

        InitialisiereVorlagenauswahl()
        InitialisiereSchenkel()
        InitialisiereGrid()

        btnZeileHinzufuegen.Text = "Zeile hinzufügen"
        btnZeileHinzufuegen.Size = New Size(130, 30)
        btnZeileHinzufuegen.Location = New Point(20, 317)
        btnZeileHinzufuegen.Anchor = AnchorStyles.Top Or AnchorStyles.Left

        btnZeileDuplizieren.Text = "Duplizieren"
        btnZeileDuplizieren.Size = New Size(110, 30)
        btnZeileDuplizieren.Location = New Point(156, 317)
        btnZeileDuplizieren.Anchor = AnchorStyles.Top Or AnchorStyles.Left

        btnZeileLoeschen.Text = "Zeile löschen"
        btnZeileLoeschen.Size = New Size(120, 30)
        btnZeileLoeschen.Location = New Point(272, 317)
        btnZeileLoeschen.Anchor = AnchorStyles.Top Or AnchorStyles.Left

        btnAlleErstellen.Text = "Alle erstellen"
        btnAlleErstellen.Size = New Size(170, 34)
        btnAlleErstellen.Location = New Point(20, 357)
        btnAlleErstellen.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnAlleErstellen.Anchor = AnchorStyles.Top Or AnchorStyles.Left

        btnBeenden.Text = "Beenden"
        btnBeenden.Size = New Size(110, 34)
        btnBeenden.Location = New Point(196, 357)
        btnBeenden.Anchor = AnchorStyles.Top Or AnchorStyles.Left
        btnBeenden.DialogResult = DialogResult.Cancel

        lblProtokoll.Text = "Protokoll:"
        lblProtokoll.Location = New Point(20, 402)
        lblProtokoll.AutoSize = True
        lblProtokoll.Anchor = AnchorStyles.Top Or AnchorStyles.Left

        txtProtokoll.Multiline = True
        txtProtokoll.ReadOnly = True
        txtProtokoll.ScrollBars = ScrollBars.Vertical
        txtProtokoll.Location = New Point(20, 422)
        txtProtokoll.Size = New Size(640, 230)
        txtProtokoll.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        txtProtokoll.Font = New Font("Consolas", 8.5F)
        txtProtokoll.BackColor = SystemColors.Window

        Me.Controls.AddRange(New Control() {
            lblUeberschrift,
            lblVorlage, lblVorlagePfad, btnVorlageWaehlen, btnVorlageStandard,
            lblSchenkel, txtSchenkel,
            gridDatensaetze,
            btnZeileHinzufuegen, btnZeileDuplizieren, btnZeileLoeschen,
            btnAlleErstellen, btnBeenden, lblProtokoll, txtProtokoll})

        Me.AcceptButton = btnAlleErstellen
        Me.CancelButton = btnBeenden
    End Sub

    ''' <summary>
    ''' Zeile fuer die Standardvorlage: Pfadanzeige + "Durchsuchen" + "Standard".
    ''' Die Pfadanzeige ist bewusst ein Label (nicht anklick- oder editierbar).
    ''' Ein leeres Feld bedeutet bewusst "Standardvorlage von Solid Edge".
    ''' </summary>
    Private Sub InitialisiereVorlagenauswahl()
        lblVorlage.Text = "Standardvorlage (Solid Edge Kantteil *" & Einstellungen.VorlagenEndung & "):"
        lblVorlage.Location = New Point(20, 45)
        lblVorlage.AutoSize = True
        lblVorlage.Anchor = AnchorStyles.Top Or AnchorStyles.Left

        lblVorlagePfad.Location = New Point(20, 64)
        lblVorlagePfad.Size = New Size(430, 23)
        lblVorlagePfad.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        lblVorlagePfad.BackColor = SystemColors.Window
        lblVorlagePfad.AutoSize = False
        lblVorlagePfad.BorderStyle = BorderStyle.Fixed3D
        lblVorlagePfad.TextAlign = ContentAlignment.MiddleLeft
        lblVorlagePfad.AutoEllipsis = True          ' lange Pfade mit "…" kuerzen
        lblVorlagePfad.UseMnemonic = False          ' "&" im Pfad (z. B. "GmbH & Co.") woertlich anzeigen

        btnVorlageWaehlen.Text = "Durchsuchen …"
        btnVorlageWaehlen.Size = New Size(110, 25)
        btnVorlageWaehlen.Location = New Point(456, 63)
        btnVorlageWaehlen.Anchor = AnchorStyles.Top Or AnchorStyles.Right

        btnVorlageStandard.Text = "Standard"
        btnVorlageStandard.Size = New Size(88, 25)
        btnVorlageStandard.Location = New Point(572, 63)
        btnVorlageStandard.Anchor = AnchorStyles.Top Or AnchorStyles.Right
    End Sub

    ''' <summary>Tooltip zeigt immer den vollstaendigen (evtl. gekuerzt dargestellten) Pfad.</summary>
    Private Sub LblVorlagePfad_TextChanged(sender As Object, e As EventArgs) Handles lblVorlagePfad.TextChanged
        tipVorlagePfad.SetToolTip(lblVorlagePfad,
            If(lblVorlagePfad.Text.Length = 0, "Standardvorlage von Solid Edge", lblVorlagePfad.Text))
    End Sub

    ''' <summary>
    ''' Eingabefeld fuer den Schenkel S: Laenge der beiden unteren Flanschlinien
    ''' am Hauptteil. Gilt fuer den gesamten Batch (nicht fuer den Deckel).
    ''' </summary>
    Private Sub InitialisiereSchenkel()
        lblSchenkel.Text = "Schenkel S [mm] (Hauptteil, alle Zeilen):"
        lblSchenkel.Location = New Point(20, 101)
        lblSchenkel.AutoSize = True
        lblSchenkel.Anchor = AnchorStyles.Top Or AnchorStyles.Left

        txtSchenkel.Location = New Point(300, 98)
        txtSchenkel.Size = New Size(70, 23)
        txtSchenkel.TextAlign = HorizontalAlignment.Right
        txtSchenkel.Anchor = AnchorStyles.Top Or AnchorStyles.Left
    End Sub

    Private Sub InitialisiereGrid()
        gridDatensaetze.Location = New Point(20, 132)
        gridDatensaetze.Size = New Size(640, 175)
        gridDatensaetze.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        gridDatensaetze.AllowUserToAddRows = False
        gridDatensaetze.AllowUserToResizeRows = False
        gridDatensaetze.RowHeadersWidth = 28
        gridDatensaetze.SelectionMode = DataGridViewSelectionMode.CellSelect
        gridDatensaetze.MultiSelect = True
        gridDatensaetze.AutoGenerateColumns = False
        gridDatensaetze.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
        gridDatensaetze.BackgroundColor = SystemColors.Window

        colBreite.HeaderText = "Breite B [mm]"
        colBreite.Width = 100
        colBreite.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight

        colTiefe.HeaderText = "Tiefe T [mm]"
        colTiefe.Width = 100
        colTiefe.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight

        colLaenge.HeaderText = "Länge L [mm]"
        colLaenge.Width = 100
        colLaenge.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight

        colDeckel.HeaderText = "Deckel"
        colDeckel.Width = 60

        colStatus.HeaderText = "Status"
        colStatus.ReadOnly = True
        colStatus.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        colStatus.DefaultCellStyle.ForeColor = SystemColors.GrayText

        gridDatensaetze.Columns.AddRange(New DataGridViewColumn() {
            colBreite, colTiefe, colLaenge, colDeckel, colStatus})
    End Sub

    ' --- Vorlagenauswahl ---------------------------------------------------

    Private Sub BtnVorlageWaehlen_Click(sender As Object, e As EventArgs) Handles btnVorlageWaehlen.Click
        Using dialog As New OpenFileDialog()
            dialog.Title = "Standardvorlage für die Blechteile auswählen"
            dialog.Filter = "Solid Edge Kantteil (*" & Einstellungen.VorlagenEndung & ")|*" &
                            Einstellungen.VorlagenEndung & "|Alle Dateien (*.*)|*.*"
            dialog.CheckFileExists = True
            dialog.Multiselect = False

            Dim startordner As String = VorlagenStartordner()
            If startordner.Length > 0 Then dialog.InitialDirectory = startordner

            Dim aktuell As String = lblVorlagePfad.Text.Trim()
            If aktuell.Length > 0 AndAlso File.Exists(aktuell) Then dialog.FileName = aktuell

            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim fehler As String = Einstellungen.PruefeVorlage(dialog.FileName)
            If fehler IsNot Nothing Then
                MessageBox.Show(fehler, "AutoBOX – Vorlage", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            lblVorlagePfad.Text = dialog.FileName
        End Using

        SpeichereEinstellungen()
        ZeigeVorlagenHinweis()
    End Sub

    Private Sub BtnVorlageStandard_Click(sender As Object, e As EventArgs) Handles btnVorlageStandard.Click
        lblVorlagePfad.Text = String.Empty
        SpeichereEinstellungen()
        ZeigeVorlagenHinweis()
    End Sub

    ''' <summary>
    ''' Sinnvoller Startordner fuer die Dateiauswahl: zuerst der Ordner der
    ''' aktuell eingetragenen Vorlage, sonst der Vorlagenordner einer
    ''' installierten Solid-Edge-Version.
    ''' </summary>
    Private Function VorlagenStartordner() As String
        Try
            Dim aktuell As String = lblVorlagePfad.Text.Trim()
            If aktuell.Length > 0 Then
                Dim ordner As String = Path.GetDirectoryName(aktuell)
                If Not String.IsNullOrEmpty(ordner) AndAlso Directory.Exists(ordner) Then Return ordner
            End If
        Catch
            ' ungueltiger Pfad im Eingabefeld – dann eben der Solid-Edge-Ordner
        End Try

        Try
            For Each programme As String In New String() {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}
                If String.IsNullOrEmpty(programme) Then Continue For
                Dim siemens As String = Path.Combine(programme, "Siemens")
                If Not Directory.Exists(siemens) Then Continue For

                For Each version As String In Directory.GetDirectories(siemens, "Solid Edge*")
                    Dim vorlagen As String = Path.Combine(version, "Template")
                    If Directory.Exists(vorlagen) Then Return vorlagen
                Next
            Next
        Catch
            ' Suche ist nur Komfort – Fehler hier sind unkritisch.
        End Try

        Return String.Empty
    End Function

    ''' <summary>
    ''' Uebernimmt Vorlage und Schenkel in die gespeicherten Einstellungen.
    ''' Ein ungueltiger Schenkel wird nicht gespeichert (der letzte gueltige bleibt).
    ''' </summary>
    Private Sub SpeichereEinstellungen()
        _einstellungen.VorlagePfad = lblVorlagePfad.Text.Trim()
        Dim schenkel As Double
        If TryParseMillimeter(txtSchenkel.Text, schenkel) AndAlso schenkel > 0 Then
            _einstellungen.SchenkelMm = schenkel
        End If
        _einstellungen.Speichern()
    End Sub

    ''' <summary>Schreibt die aktuell wirksame Vorlage ins Protokoll.</summary>
    Private Sub ZeigeVorlagenHinweis()
        Dim vorlage As String = lblVorlagePfad.Text.Trim()
        If vorlage.Length = 0 Then
            Protokolliere("Vorlage: keine gewählt – es wird die Standardvorlage von Solid Edge verwendet.")
        ElseIf Not File.Exists(vorlage) Then
            Protokolliere("Vorlage: " & vorlage & "  (ACHTUNG: Datei nicht gefunden)")
        Else
            Protokolliere("Vorlage: " & vorlage)
        End If
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        SpeichereEinstellungen()
    End Sub

    ' --- Zeilenverwaltung -------------------------------------------------

    ''' <summary>Fuegt eine Zeile mit den bisherigen Standardwerten hinzu und gibt ihren Index zurueck.</summary>
    Private Function FuegeStandardzeile() As Integer
        Return FuegeZeileHinzu("300", "200", "400", False)
    End Function

    Private Function FuegeZeileHinzu(breite As String, tiefe As String, laenge As String, deckel As Boolean) As Integer
        Dim index As Integer = gridDatensaetze.Rows.Add()
        Dim row As DataGridViewRow = gridDatensaetze.Rows(index)
        row.Cells(colBreite.Index).Value = breite
        row.Cells(colTiefe.Index).Value = tiefe
        row.Cells(colLaenge.Index).Value = laenge
        row.Cells(colDeckel.Index).Value = deckel
        row.Cells(colStatus.Index).Value = ""
        Return index
    End Function

    Private Sub BtnZeileHinzufuegen_Click(sender As Object, e As EventArgs) Handles btnZeileHinzufuegen.Click
        Dim index As Integer = FuegeStandardzeile()
        gridDatensaetze.ClearSelection()
        gridDatensaetze.CurrentCell = gridDatensaetze.Rows(index).Cells(colBreite.Index)
    End Sub

    Private Sub BtnZeileDuplizieren_Click(sender As Object, e As EventArgs) Handles btnZeileDuplizieren.Click
        Dim quelle As DataGridViewRow = AktuelleZeile()
        If quelle Is Nothing Then
            Protokolliere("Duplizieren: keine Zeile ausgewählt.")
            Return
        End If
        Dim index As Integer = FuegeZeileHinzu(
            LiesZelle(quelle, colBreite),
            LiesZelle(quelle, colTiefe),
            LiesZelle(quelle, colLaenge),
            LiesDeckel(quelle))
        gridDatensaetze.ClearSelection()
        gridDatensaetze.CurrentCell = gridDatensaetze.Rows(index).Cells(colBreite.Index)
    End Sub

    Private Sub BtnZeileLoeschen_Click(sender As Object, e As EventArgs) Handles btnZeileLoeschen.Click
        ' Alle Zeilen entfernen, die eine markierte Zelle enthalten (bzw. die aktuelle Zeile).
        Dim zuLoeschen As New List(Of DataGridViewRow)
        For Each zelle As DataGridViewCell In gridDatensaetze.SelectedCells
            If Not zuLoeschen.Contains(zelle.OwningRow) Then zuLoeschen.Add(zelle.OwningRow)
        Next
        If zuLoeschen.Count = 0 AndAlso AktuelleZeile() IsNot Nothing Then
            zuLoeschen.Add(AktuelleZeile())
        End If

        If zuLoeschen.Count = 0 Then
            Protokolliere("Löschen: keine Zeile ausgewählt.")
            Return
        End If

        For Each row As DataGridViewRow In zuLoeschen
            If Not row.IsNewRow Then gridDatensaetze.Rows.Remove(row)
        Next
    End Sub

    Private Function AktuelleZeile() As DataGridViewRow
        If gridDatensaetze.CurrentRow IsNot Nothing AndAlso Not gridDatensaetze.CurrentRow.IsNewRow Then
            Return gridDatensaetze.CurrentRow
        End If
        Return Nothing
    End Function

    ' --- Batch ausfuehren -------------------------------------------------

    Private Sub BtnAlleErstellen_Click(sender As Object, e As EventArgs) Handles btnAlleErstellen.Click
        ' Laufende Editierung uebernehmen, damit die zuletzt getippte Zelle mitzaehlt.
        gridDatensaetze.EndEdit()

        If gridDatensaetze.Rows.Count = 0 Then
            MessageBox.Show("Die Liste ist leer. Bitte mindestens eine Zeile hinzufügen.",
                            "AutoBOX", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' 0) Vorlage pruefen – sie gilt fuer alle Zeilen des Batches.
        Dim vorlage As String = lblVorlagePfad.Text.Trim()
        Dim vorlagenFehler As String = Einstellungen.PruefeVorlage(vorlage)
        If vorlagenFehler IsNot Nothing Then
            Protokolliere("Vorlage ungültig – Batch nicht gestartet: " & vorlagenFehler)
            MessageBox.Show(vorlagenFehler & Environment.NewLine & Environment.NewLine &
                            "Bitte eine gültige Vorlage wählen oder mit ""Standard"" die " &
                            "Standardvorlage von Solid Edge verwenden.",
                            "AutoBOX – Vorlage", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Schenkel pruefen – er gilt ebenfalls fuer alle Zeilen des Batches.
        Dim schenkelMm As Double
        Dim schenkelFehler As String = Nothing
        If Not TryParseMillimeter(txtSchenkel.Text, schenkelMm) Then
            schenkelFehler = "Schenkel (S) ist keine gültige Zahl."
        ElseIf schenkelMm <= 0 OrElse schenkelMm > 10000.0 Then
            schenkelFehler = "Schenkel (S) muss größer als 0 mm und höchstens 10000 mm sein."
        End If
        If schenkelFehler IsNot Nothing Then
            Protokolliere("Schenkel ungültig – Batch nicht gestartet: " & schenkelFehler)
            MessageBox.Show(schenkelFehler, "AutoBOX – Schenkel", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtSchenkel.Focus()
            Return
        End If
        SpeichereEinstellungen()

        ' 1) Alle Zeilen einlesen und pruefen. Ungueltige Zeilen werden markiert
        '    und uebersprungen; der Batch laeuft mit den gueltigen Zeilen weiter.
        Dim datensaetze As New List(Of BoxParameter)
        Dim zeilenIndex As New List(Of Integer)   ' Grid-Zeilenindex je gueltigem Datensatz
        Dim uebersprungen As Integer = 0

        For Each row As DataGridViewRow In gridDatensaetze.Rows
            If row.IsNewRow Then Continue For

            Dim parameter As New BoxParameter()
            Dim fehler As New List(Of String)
            Dim wert As Double

            If TryParseMillimeter(LiesZelle(row, colBreite), wert) Then parameter.BreiteB = wert Else fehler.Add("Breite (B) ist keine gültige Zahl")
            If TryParseMillimeter(LiesZelle(row, colTiefe), wert) Then parameter.TiefeT = wert Else fehler.Add("Tiefe (T) ist keine gültige Zahl")
            If TryParseMillimeter(LiesZelle(row, colLaenge), wert) Then parameter.LaengeL = wert Else fehler.Add("Länge (L) ist keine gültige Zahl")
            parameter.MitDeckel = LiesDeckel(row)
            parameter.SchenkelMm = schenkelMm

            If fehler.Count = 0 Then fehler.AddRange(parameter.PruefeGrundwerte())

            If fehler.Count > 0 Then
                Dim meldung As String = String.Join("; ", fehler)
                SetzeStatus(row, "ÜBERSPRUNGEN: " & meldung, StatusFarbe.Fehler)
                Protokolliere("Zeile " & (row.Index + 1) & " übersprungen – " & meldung)
                uebersprungen += 1
            Else
                SetzeStatus(row, "wartet …", StatusFarbe.Neutral)
                datensaetze.Add(parameter)
                zeilenIndex.Add(row.Index)
            End If
        Next

        If datensaetze.Count = 0 Then
            Protokolliere("Keine gültigen Datensätze vorhanden – Batch nicht gestartet.")
            MessageBox.Show("Es gibt keine gültigen Datensätze zum Erstellen." & Environment.NewLine &
                            "Bitte die markierten Zeilen korrigieren.",
                            "AutoBOX – Keine gültigen Datensätze", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' 2) Batch ausfuehren (einmalige Solid-Edge-Verbindung)
        SetzeBedienbarkeit(False)
        Dim erfolgreich As Integer = 0
        Dim fehlgeschlagen As Integer = 0
        Try
            Protokolliere("Starte Batch mit " & datensaetze.Count & " Datensatz(en)" &
                          If(uebersprungen > 0, " (" & uebersprungen & " übersprungen)", "") &
                          ", Schenkel S = " & schenkelMm & " mm …")

            Dim automation As New SolidEdgeAutomation(AddressOf Protokolliere, vorlage)
            automation.ErzeugeBatch(datensaetze,
                Sub(index As Integer, erfolg As Boolean, meldung As String)
                    Dim row As DataGridViewRow = gridDatensaetze.Rows(zeilenIndex(index))
                    If erfolg Then
                        SetzeStatus(row, "OK", StatusFarbe.Erfolg)
                        erfolgreich += 1
                    Else
                        SetzeStatus(row, "FEHLER: " & meldung, StatusFarbe.Fehler)
                        fehlgeschlagen += 1
                    End If
                    gridDatensaetze.Update()
                    txtProtokoll.Update()
                End Sub)

            Dim gesamtFehler As Integer = fehlgeschlagen + uebersprungen
            Protokolliere("Batch fertig – " & erfolgreich & " erfolgreich, " & gesamtFehler & " fehlerhaft/übersprungen.")
            MessageBox.Show(
                "Stapelverarbeitung abgeschlossen." & Environment.NewLine & Environment.NewLine &
                "Erfolgreich:            " & erfolgreich & Environment.NewLine &
                "Fehlgeschlagen:         " & fehlgeschlagen & Environment.NewLine &
                "Übersprungen (Eingabe): " & uebersprungen,
                "AutoBOX – Batch abgeschlossen", MessageBoxButtons.OK,
                If(gesamtFehler > 0, MessageBoxIcon.Warning, MessageBoxIcon.Information))
        Catch ex As Exception
            ' Ein Fehler hier (z. B. Solid-Edge-Verbindung) betrifft den ganzen Batch.
            Protokolliere("SCHWERER FEHLER: " & ex.Message)
            MessageBox.Show("Der Batch konnte nicht (vollständig) ausgeführt werden:" & Environment.NewLine &
                            Environment.NewLine & ex.Message,
                            "AutoBOX – Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            SetzeBedienbarkeit(True)
        End Try
    End Sub

    ' --- Hilfen -----------------------------------------------------------

    ''' <summary>Liest den getrimmten Text einer Zelle (Nothing wird zu "").</summary>
    Private Function LiesZelle(row As DataGridViewRow, spalte As DataGridViewColumn) As String
        Dim wert As Object = row.Cells(spalte.Index).Value
        If wert Is Nothing Then Return String.Empty
        Return wert.ToString().Trim()
    End Function

    ''' <summary>Liest den Deckel-Haken einer Zeile (Nothing wird zu False).</summary>
    Private Function LiesDeckel(row As DataGridViewRow) As Boolean
        Dim wert As Object = row.Cells(colDeckel.Index).Value
        Return wert IsNot Nothing AndAlso CBool(wert)
    End Function

    Private Enum StatusFarbe
        Neutral
        Erfolg
        Fehler
    End Enum

    Private Sub SetzeStatus(row As DataGridViewRow, text As String, farbe As StatusFarbe)
        Dim zelle As DataGridViewCell = row.Cells(colStatus.Index)
        zelle.Value = text
        Select Case farbe
            Case StatusFarbe.Erfolg
                zelle.Style.ForeColor = Color.Green
            Case StatusFarbe.Fehler
                zelle.Style.ForeColor = Color.Firebrick
            Case Else
                zelle.Style.ForeColor = SystemColors.GrayText
        End Select
    End Sub

    ''' <summary>Zahlen mit Komma ("2,5") und Punkt ("2.5") akzeptieren.</summary>
    Private Function TryParseMillimeter(eingabe As String, ByRef wert As Double) As Boolean
        eingabe = eingabe.Trim()
        If eingabe.Length = 0 Then Return False
        If Double.TryParse(eingabe, NumberStyles.Float, CultureInfo.CurrentCulture, wert) Then Return True
        Return Double.TryParse(eingabe.Replace(","c, "."c), NumberStyles.Float, CultureInfo.InvariantCulture, wert)
    End Function

    Private Sub Protokolliere(text As String)
        txtProtokoll.AppendText(DateTime.Now.ToString("HH:mm:ss") & "  " & text & Environment.NewLine)
        txtProtokoll.Update()
    End Sub

    Private Sub SetzeBedienbarkeit(aktiv As Boolean)
        gridDatensaetze.Enabled = aktiv
        txtSchenkel.Enabled = aktiv
        btnVorlageWaehlen.Enabled = aktiv
        btnVorlageStandard.Enabled = aktiv
        btnZeileHinzufuegen.Enabled = aktiv
        btnZeileDuplizieren.Enabled = aktiv
        btnZeileLoeschen.Enabled = aktiv
        btnAlleErstellen.Enabled = aktiv
        Me.UseWaitCursor = Not aktiv
        Me.Cursor = If(aktiv, Cursors.Default, Cursors.WaitCursor)
    End Sub

    Private Sub BtnBeenden_Click(sender As Object, e As EventArgs) Handles btnBeenden.Click
        Me.Close()
    End Sub

End Class
