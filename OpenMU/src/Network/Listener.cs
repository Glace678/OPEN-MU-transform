// <copyright file="Listener.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.IO.Pipelines;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;
using Pipelines.Sockets.Unofficial;

/// <summary>
/// A tcp listener which automatically creates instances of <see cref="Connection"/>s for accepted clients.
/// </summary>
public class Listener
{
    private readonly ILogger _logger;
    private readonly int _port;
    private readonly Func<PipeReader, IPipelinedDecryptor?>? _decryptorCreator;
    private readonly Func<PipeWriter, IPipelinedEncryptor?>? _encryptorCreator;
    private readonly ILoggerFactory _loggerFactory;
    private TcpListener? _clientListener;
    private volatile bool _isListening;

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener" /> class.
    /// </summary>
    /// <param name="port">The port on which the tcp listener should listen to.</param>
    /// <param name="decryptorCreator">The decryptor creator function.</param>
    /// <param name="encryptorCreator">The encryptor creator function.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public Listener(int port, Func<PipeReader, IPipelinedDecryptor?>? decryptorCreator, Func<PipeWriter, IPipelinedEncryptor?>? encryptorCreator, ILoggerFactory loggerFactory)
    {
        this._port = port;
        this._decryptorCreator = decryptorCreator;
        this._encryptorCreator = encryptorCreator;
        this._loggerFactory = loggerFactory;

        this._logger = this._loggerFactory.CreateLogger<Listener>();
    }

    /// <summary>
    /// Occurs when a client has been accepted by the tcp listener.
    /// </summary>
    public event AsyncEventHandler<ClientAcceptedEventArgs>? ClientAccepted;

    /// <summary>
    /// Occurs when a client has been accepted by the tcp listener, but before a <see cref="Connection"/> is created.
    /// </summary>
    public event AsyncEventHandler<ClientAcceptingEventArgs>? ClientAccepting;

    /// <summary>
    /// Gets a value indicating whether this listener is bound to a specific local port.
    /// </summary>
    public bool IsBound => this._isListening;

    /// <summary>
    /// Starts the tcp listener and begins to accept connections.
    /// </summary>
    /// <param name="backlog">The maximum length of the pending connections queue.</param>
    public void Start(int backlog = (int)SocketOptionName.MaxConnections)
    {
        var address = ListenerAddressResolver.Resolve();
        // Windows permits a same-user specific bind to shadow a non-exclusive wildcard listener.
        if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint =>
                endpoint.Port == this._port && endpoint.AddressFamily == AddressFamily.InterNetwork
                && (address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(address))))
        {
            throw new SocketException((int)SocketError.AddressAlreadyInUse);
        }

        this._clientListener = new TcpListener(address, this._port) { ExclusiveAddressUse = true };
        try
        {
            this._clientListener.Start(backlog);
            this._isListening = true;
            _ = this.AcceptClientsAsync(this._clientListener);
        }
        catch
        {
            this._isListening = false;
            this._clientListener.Stop();
            throw;
        }
    }

    /// <summary>
    /// Stops the tcp listener.
    /// </summary>
    public void Stop()
    {
        this._isListening = false;
        // Detach once so overlapping shutdown calls cannot stop the same listener twice.
        var listener = System.Threading.Interlocked.Exchange(ref this._clientListener, null);
        listener?.Stop();
    }

    /// <summary>
    /// Creates the decryptor for the specified reader.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The created decryptor.</returns>
    protected virtual IPipelinedDecryptor? CreateDecryptor(PipeReader reader)
    {
        return this._decryptorCreator?.Invoke(reader);
    }

    /// <summary>
    /// Creates the encryptor for the specified writer.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <returns>The created encryptor.</returns>
    protected virtual IPipelinedEncryptor? CreateEncryptor(PipeWriter writer)
    {
        return this._encryptorCreator?.Invoke(writer);
    }

    private IConnection CreateConnection(Socket clientSocket)
    {
        var socketConnection = SocketConnection.Create(clientSocket);
        return new Connection(socketConnection, this.CreateDecryptor(socketConnection.Input), this.CreateEncryptor(socketConnection.Output), this._loggerFactory.CreateLogger<Connection>());
    }

    private async Task AcceptClientsAsync(TcpListener listener)
    {
        try
        {
            while (this._isListening && ReferenceEquals(listener, this._clientListener))
            {
                Socket socket;
                try
                {
                    socket = await listener.AcceptSocketAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    // this exception is expected when the clientListener got disposed. In this case we don't want to spam the log.
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    this._logger.LogDebug(ex, "The listener was stopped.");
                    return;
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Error accepting the client socket");
                    return;
                }

                _ = this.HandleClientAsync(socket);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Unexpected error while accepting clients.");
        }
    }

    private async Task HandleClientAsync(Socket socket)
    {
        try
        {
            ClientAcceptingEventArgs? cancel = null;
            if (this.ClientAccepting is { } clientAccepting)
            {
                cancel = new ClientAcceptingEventArgs(socket);
                await clientAccepting.Invoke(cancel).ConfigureAwait(false);
            }

            if (cancel is not null && cancel.Cancel)
            {
                socket.Dispose();
                return;
            }

            socket.NoDelay = true;
            IConnection connection;
            try
            {
                connection = this.CreateConnection(socket);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error creating connection for accepted socket; cleaning up.");
                socket.Dispose();
                return;
            }

            try
            {
                if (this.ClientAccepted is { } clientAccepted)
                {
                    await clientAccepted.Invoke(new ClientAcceptedEventArgs(connection)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "ClientAccepted callback failed; disconnecting the connection.");
                try
                {
                    await connection.DisconnectAsync().ConfigureAwait(false);
                }
                catch (Exception inner)
                {
                    this._logger.LogError(inner, "Error while disconnecting failed connection.");
                }

                connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Unexpected error while handling an accepted client.");
        }
    }
}
