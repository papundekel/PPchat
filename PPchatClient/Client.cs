﻿using System.Collections.Generic;
using System.Net;
using System.Linq;
using PPchatLibrary;
using PPchatClient.Commands;
using System.IO;
using System;

namespace PPchatClient
{
	public class Client : Application<Client>, IDisposable,
		ICommandHandler<ChangePortCommandArgument>,
		ICommandHandler<ConnectCommandArgument>,
		ICommandHandler<DefaultConnectCommandArgument>,
		ICommandHandler<DisconnectCommandArgument>,
		ICommandHandler<PrintPortCommandArgument>,
		ICommandHandler<SayCommandArgument>,
		ICommandHandler<SimpleConnectAddressCommandArgument>,
		ICommandHandler<SimpleConnectPortCommandArgument>,
		ICommandHandler<SimpleConnectSavedServerCommandArgument>,
		ICommandHandler<SaveServerCommandArgument>,
		ICommandHandler<ListSavedServersCommandArgument>
	{
		IConnection? connection;

		bool Connected => connection != null;

		protected override IEnumerable<IConnection> Connections => connection?.AsSingleEnumerable() ?? Enumerable.Empty<IConnection>();

		protected override string ExitMessage => "disconnecting because the client is shutting down";

		int defaultPort = 2048;

		const string savedServersPath = "saved_servers";

		readonly IDictionary<string, (IPAddress, int)> savedServers;

		void SerializeSavedServers()
		{
			File.Delete(savedServersPath);

			using var serializer = new SimpleSerializerStream(new FileStream(savedServersPath, FileMode.CreateNew));

			foreach (var (key, value) in savedServers)
			{
				serializer.Write(key);
				serializer.Write(value);
			}
		}
		static IDictionary<string, (IPAddress, int)> DeserializeSavedServers()
		{
			IDictionary<string, (IPAddress, int)> saved_servers = new Dictionary<string, (IPAddress, int)>();
			
			if (File.Exists(savedServersPath))
			{
				using var serializer = new SimpleSerializerStream(new FileStream(savedServersPath, FileMode.Open));

				(string key, (IPAddress, int) value) tuple;

				while (true)
				{
					try	{ tuple.key = serializer.Read<string>(); }
					catch (SimpleSerializerEndOfStreamException) { break; }

					tuple.value = serializer.Read<(IPAddress, int)>();

					saved_servers.Add(tuple.key, tuple.value);
				}
			}

			return saved_servers;
		}

		public Client()
		{
			savedServers = DeserializeSavedServers();
		}

		public override void RemoveConnection(IConnection _)
		{
			connection = null;
		}

		internal void ChangePort(int port)
		{
			Write($"the default port was changed from: {defaultPort}, to: {port}");
			defaultPort = port;
		}

		protected override void HandleAfterExit()
		{}

		protected override void ClearConnections()
		{
			connection = null;
		}

		public override void HandleAbruptConnectionClose(IConnection _)
		{
			Write("server abruptly terminated the connection");
		}

		public override void HandleNormalConnectionClose(IConnection _, string reason)
		{
			Write($"server terminated the connection, reason: {reason}");
		}

		public void Handle(ChangePortCommandArgument argument)
		{
			Write($"the default port was changed from: {defaultPort}, to: {argument.port}");
			defaultPort = argument.port;
		}

		public void Handle(PrintPortCommandArgument _)
		{
			Write($"the default port is: {defaultPort}");
		}

		public void Handle(DisconnectCommandArgument _)
		{
			if (Connected)
			{
				CloseAllConnections("disconnecting by command");
				Write("disconnected");
			}
			else
				Write("you're not connected to any server");
		}

		public void Connect(IPAddress ipAddress, int port)
		{
			try
			{
				connection = new ClientConnection(this, ipAddress, port);
				Write("enter your username:");
				var username = Read();
				connection.Stream.Write(new LoginPacket(username));
			}
			catch
			{
				connection = null;
				Write("couldn't connect");
			}
		}
		public void Handle(ConnectCommandArgument argument)
		{
			Connect(argument.ipAddress, argument.port);
		}
		public void Handle(SimpleConnectPortCommandArgument argument)
		{
			Connect(IPAddress.Loopback, argument.Port);
		}
		public void Handle(SimpleConnectAddressCommandArgument argument)
		{
			Connect(argument.Address, defaultPort);
		}
		public void Handle(DefaultConnectCommandArgument _)
		{
			Connect(IPAddress.Loopback, defaultPort);
		}

		void SendMessage(string message)
		{
			if (Connected)
			{
				connection!.Stream.Write(new MessageForServerPacket(message));
			}
			else
			{
				Write("you are not connected");
			}
		}

		public void Handle(SayCommandArgument argument) => SendMessage(argument.Message);

		public override void Handle(NotFoundCommandArgument argument) => SendMessage(argument.Input);

		public void Handle(SaveServerCommandArgument argument)
		{
			savedServers.Add(argument.ServerName, (argument.Address, argument.Port));
		}

		public void Handle(SimpleConnectSavedServerCommandArgument argument)
		{
			if (savedServers.TryGetValue(argument.ServerName, out (IPAddress address, int port) pair))
				Connect(pair.address, pair.port);
			else
				Write($"There is no saved server named {argument.ServerName}");
		}

		public void Handle(ListSavedServersCommandArgument argument)
		{
			if (savedServers.Any())
			{
				Write("your saved servers are:");
				foreach (var (name, (address, port)) in savedServers)
					Write($"{name}: {address}, {port}");
			}
			else
			{
				Write("you don't have any saved servers");
			}
		}

		public void Dispose()
		{
			SerializeSavedServers();
		}
	}
}
