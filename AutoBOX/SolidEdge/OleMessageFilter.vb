Option Strict On
Option Explicit On

Imports System
Imports System.Runtime.InteropServices

''' <summary>
''' OLE-Message-Filter fuer die COM-Automatisierung von Solid Edge.
'''
''' Hintergrund: Ist Solid Edge gerade beschaeftigt (z. B. waehrend
''' Documents.Add eine Vorlage laedt), lehnt es eingehende COM-Aufrufe ab.
''' Ohne Message-Filter wirft .NET dann sofort
''' RPC_E_CALL_REJECTED (0x80010001) bzw. RPC_E_SERVERCALL_RETRYLATER.
''' Mit diesem Filter wird der abgelehnte Aufruf automatisch wiederholt,
''' bis Solid Edge wieder bereit ist (oder das Zeitlimit ablaeuft).
'''
''' Wichtig: Der Filter wirkt nur im STA-Thread, in dem er registriert wurde
''' (hier: der UI-Thread mit &lt;STAThread&gt;).
'''
''' Verwendung:
'''   Using filter As New OleMessageFilter()
'''       ... COM-Aufrufe ...
'''   End Using
''' </summary>
Public NotInheritable Class OleMessageFilter
    Implements IOleMessageFilter, IDisposable

    ' Rueckgabewerte laut COM-Dokumentation (IMessageFilter)
    Private Const SERVERCALL_ISHANDLED As Integer = 0
    Private Const SERVERCALL_REJECTED As Integer = 1
    Private Const SERVERCALL_RETRYLATER As Integer = 2
    Private Const PENDINGMSG_WAITDEFPROCESS As Integer = 2

    ''' <summary>Wartezeit in ms bis zur Wiederholung eines abgelehnten Aufrufs.</summary>
    Private Const WIEDERHOLEN_NACH_MS As Integer = 100

    ''' <summary>
    ''' Gesamtzeit in ms, nach der ein dauerhaft abgelehnter Aufruf aufgegeben wird
    ''' (verhindert ein endloses Haengen, falls Solid Edge z. B. einen Dialog zeigt).
    ''' </summary>
    Private Const ZEITLIMIT_MS As Integer = 120000

    Private _vorherigerFilter As IOleMessageFilter
    Private _registriert As Boolean

    ''' <summary>Registriert den Filter fuer den aktuellen (STA-)Thread.</summary>
    Public Sub New()
        Dim alt As IOleMessageFilter = Nothing
        Dim hr As Integer = CoRegisterMessageFilter(Me, alt)
        ' hr <> 0: z. B. MTA-Thread – dann ohne Filter weiterarbeiten (wie bisher).
        _registriert = (hr = 0)
        _vorherigerFilter = alt
    End Sub

    ''' <summary>Meldet den Filter ab und stellt den vorherigen wieder her.</summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        If Not _registriert Then Return
        _registriert = False
        Dim ignoriert As IOleMessageFilter = Nothing
        CoRegisterMessageFilter(_vorherigerFilter, ignoriert)
        _vorherigerFilter = Nothing
    End Sub

    Private Function HandleInComingCall(dwCallType As Integer, hTaskCaller As IntPtr,
                                        dwTickCount As Integer, lpInterfaceInfo As IntPtr) As Integer _
        Implements IOleMessageFilter.HandleInComingCall
        Return SERVERCALL_ISHANDLED
    End Function

    Private Function RetryRejectedCall(hTaskCallee As IntPtr, dwTickCount As Integer,
                                       dwRejectType As Integer) As Integer _
        Implements IOleMessageFilter.RetryRejectedCall
        If (dwRejectType = SERVERCALL_RETRYLATER OrElse dwRejectType = SERVERCALL_REJECTED) AndAlso
           dwTickCount < ZEITLIMIT_MS Then
            Return WIEDERHOLEN_NACH_MS ' nach x ms erneut versuchen
        End If
        Return -1 ' aufgeben -> Aufrufer erhaelt die COM-Ausnahme
    End Function

    Private Function MessagePending(hTaskCallee As IntPtr, dwTickCount As Integer,
                                    dwPendingType As Integer) As Integer _
        Implements IOleMessageFilter.MessagePending
        Return PENDINGMSG_WAITDEFPROCESS
    End Function

    <DllImport("ole32.dll")>
    Private Shared Function CoRegisterMessageFilter(newFilter As IOleMessageFilter,
                                                    ByRef oldFilter As IOleMessageFilter) As Integer
    End Function

End Class

''' <summary>COM-Schnittstelle IMessageFilter (ole32).</summary>
<ComImport(), Guid("00000016-0000-0000-C000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Public Interface IOleMessageFilter
    <PreserveSig()>
    Function HandleInComingCall(dwCallType As Integer, hTaskCaller As IntPtr,
                                dwTickCount As Integer, lpInterfaceInfo As IntPtr) As Integer
    <PreserveSig()>
    Function RetryRejectedCall(hTaskCallee As IntPtr, dwTickCount As Integer,
                               dwRejectType As Integer) As Integer
    <PreserveSig()>
    Function MessagePending(hTaskCallee As IntPtr, dwTickCount As Integer,
                            dwPendingType As Integer) As Integer
End Interface
