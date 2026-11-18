Option Strict On
Option Explicit On

Imports System
Imports System.Runtime.InteropServices
Imports SolidEdgeFramework

''' <summary>
''' Automatisierungs-Schicht: Aufbau/Verwaltung der COM-Verbindung zu Solid Edge.
''' Trennt die reine Verbindungslogik von der Geometrieerzeugung.
''' </summary>
Public Class SolidEdgeConnection

    Private Const PROGID_APPLICATION As String = "SolidEdge.Application"

    ''' <summary>Umrechnung Millimeter -> Meter (die Solid-Edge-API rechnet in Metern).</summary>
    Public Const MmZuMeter As Double = 1.0 / 1000.0

    Private ReadOnly _log As Action(Of String)

    Public Sub New(log As Action(Of String))
        _log = If(log, Sub(t As String) System.Diagnostics.Debug.WriteLine(t))
    End Sub

    ''' <summary>
    ''' Verbindet mit einer laufenden Solid-Edge-Instanz oder startet Solid Edge neu.
    ''' </summary>
    Public Function Verbinde() As SolidEdgeFramework.Application
        Dim app As SolidEdgeFramework.Application

        Try
            app = CType(Marshal.GetActiveObject(PROGID_APPLICATION), SolidEdgeFramework.Application)
            _log("Mit laufender Solid-Edge-Instanz verbunden.")
        Catch ex As COMException
            _log("Keine laufende Solid-Edge-Instanz gefunden – Solid Edge wird gestartet ...")
            Dim comTyp As Type = Type.GetTypeFromProgID(PROGID_APPLICATION, throwOnError:=False)
            If comTyp Is Nothing Then
                Throw New InvalidOperationException(
                    "Solid Edge ist auf diesem Rechner nicht registriert/installiert " &
                    "(ProgID '" & PROGID_APPLICATION & "' nicht gefunden).")
            End If
            app = CType(Activator.CreateInstance(comTyp), SolidEdgeFramework.Application)
            _log("Solid Edge wurde gestartet.")
        End Try

        app.Visible = True
        Return app
    End Function

    ''' <summary>Zoomt die aktive Ansicht auf das Modell (Fehler hier sind unkritisch).</summary>
    Public Sub AnsichtEinpassen(app As SolidEdgeFramework.Application)
        Try
            Dim fenster As SolidEdgeFramework.Window = TryCast(app.ActiveWindow, SolidEdgeFramework.Window)
            If fenster IsNot Nothing Then fenster.View.Fit()
        Catch
            ' unkritisch
        End Try
    End Sub

    ''' <summary>Gibt einen COM-Wrapper best effort frei.</summary>
    Public Shared Sub GibFrei(comObjekt As Object)
        Try
            If comObjekt IsNot Nothing AndAlso Marshal.IsComObject(comObjekt) Then
                Marshal.ReleaseComObject(comObjekt)
            End If
        Catch
            ' Freigabefehler ignorieren
        End Try
    End Sub

End Class
