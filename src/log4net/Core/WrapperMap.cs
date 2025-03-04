#region Apache License
//
// Licensed to the Apache Software Foundation (ASF) under one or more 
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership. 
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with 
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Collections;

using log4net.Repository;
using log4net.Util;

namespace log4net.Core;

/// <summary>
/// Delegate used to handle creation of new wrappers.
/// </summary>
/// <param name="logger">The logger to wrap in a wrapper.</param>
/// <remarks>
/// <para>
/// Delegate used to handle creation of new wrappers. This delegate
/// is called from the <see cref="WrapperMap.CreateNewWrapperObject"/>
/// method to construct the wrapper for the specified logger.
/// </para>
/// <para>
/// The delegate to use is supplied to the <see cref="WrapperMap"/>
/// constructor.
/// </para>
/// </remarks>
public delegate ILoggerWrapper WrapperCreationHandler(ILogger logger);

/// <summary>
/// Maps between logger objects and wrapper objects.
/// </summary>
/// <remarks>
/// <para>
/// This class maintains a mapping between <see cref="ILogger"/> objects and
/// <see cref="ILoggerWrapper"/> objects. Use the <see cref="GetWrapper"/> method to 
/// look up the <see cref="ILoggerWrapper"/> for the specified <see cref="ILogger"/>.
/// </para>
/// <para>
/// New wrapper instances are created by the <see cref="CreateNewWrapperObject"/>
/// method. The default behavior is for this method to delegate construction
/// of the wrapper to the <see cref="WrapperCreationHandler"/> delegate supplied
/// to the constructor. This allows specialization of the behavior without
/// requiring subclassing of this type.
/// </para>
/// </remarks>
/// <author>Nicko Cadell</author>
/// <author>Gert Driesen</author>
public class WrapperMap
{
  private readonly object _syncRoot = new();
  /// <summary>
  /// Initializes a new instance of the <see cref="WrapperMap" />
  /// </summary>
  /// <param name="createWrapperHandler">The handler to use to create the wrapper objects.</param>
  /// <remarks>
  /// <para>
  /// Initializes a new instance of the <see cref="WrapperMap" /> class with 
  /// the specified handler to create the wrapper objects.
  /// </para>
  /// </remarks>
  public WrapperMap(WrapperCreationHandler createWrapperHandler)
  {
    _createWrapperHandler = createWrapperHandler;

    // Create the delegates for the event callbacks
    _shutdownHandler = ILoggerRepository_Shutdown;
  }

  /// <summary>
  /// Gets the wrapper object for the specified logger.
  /// </summary>
  /// <returns>The wrapper object for the specified logger</returns>
  /// <remarks>
  /// <para>
  /// If the logger is null then the corresponding wrapper is null.
  /// </para>
  /// <para>
  /// Looks up the wrapper it has previously been requested and
  /// returns it. If the wrapper has never been requested before then
  /// the <see cref="CreateNewWrapperObject"/> virtual method is
  /// called.
  /// </para>
  /// </remarks>
  public virtual ILoggerWrapper? GetWrapper(ILogger? logger)
  {
    // If the logger is null then the corresponding wrapper is null
    if (logger?.Repository is null)
    {
      return null;
    }

    lock (_syncRoot)
    {
      // Look up hierarchy in map.
      Hashtable? wrappersMap = (Hashtable?)Repositories[logger.Repository];
      if (wrappersMap is null)
      {
        // Hierarchy does not exist in map.
        // Must register with hierarchy
        wrappersMap = [];
        Repositories[logger.Repository] = wrappersMap;

        if (logger.Repository is not null)
        {
          // Register for config reset & shutdown on repository
          logger.Repository.ShutdownEvent += _shutdownHandler;
        }
      }

      // Look for the wrapper object in the map
      if (wrappersMap[logger] is not ILoggerWrapper wrapperObject)
      {
        // No wrapper object exists for the specified logger

        // Create a new wrapper wrapping the logger
        wrapperObject = CreateNewWrapperObject(logger);

        // Store wrapper logger in map
        wrappersMap[logger] = wrapperObject;
      }

      return wrapperObject;
    }
  }

  /// <summary>
  /// Gets the map of logger repositories.
  /// </summary>
  /// <value>
  /// Map of logger repositories.
  /// </value>
  /// <remarks>
  /// <para>
  /// Gets the hashtable that is keyed on <see cref="ILoggerRepository"/>. The
  /// values are hashtables keyed on <see cref="ILogger"/> with the
  /// value being the corresponding <see cref="ILoggerWrapper"/>.
  /// </para>
  /// </remarks>
  protected Hashtable Repositories { get; } = [];

  /// <summary>
  /// Creates the wrapper object for the specified logger.
  /// </summary>
  /// <param name="logger">The logger to wrap in a wrapper.</param>
  /// <returns>The wrapper object for the logger.</returns>
  /// <remarks>
  /// <para>
  /// This implementation uses the <see cref="WrapperCreationHandler"/>
  /// passed to the constructor to create the wrapper. This method
  /// can be overridden in a subclass.
  /// </para>
  /// </remarks>
  protected virtual ILoggerWrapper CreateNewWrapperObject(ILogger logger) 
    => _createWrapperHandler.Invoke(logger);

  /// <summary>
  /// Called when a monitored repository shutdown event is received.
  /// </summary>
  /// <param name="repository">The <see cref="ILoggerRepository"/> that is shutting down</param>
  /// <remarks>
  /// <para>
  /// This method is called when a <see cref="ILoggerRepository"/> that this
  /// <see cref="WrapperMap"/> is holding loggers for has signaled its shutdown
  /// event <see cref="ILoggerRepository.ShutdownEvent"/>. The default
  /// behavior of this method is to release the references to the loggers
  /// and their wrappers generated for this repository.
  /// </para>
  /// </remarks>
  protected virtual void RepositoryShutdown(ILoggerRepository repository)
  {
    lock (_syncRoot)
    {
      // Remove the repository from map
      Repositories.Remove(repository.EnsureNotNull());

      // Unhook events from the repository
      repository.ShutdownEvent -= _shutdownHandler;
    }
  }

  /// <summary>
  /// Event handler for repository shutdown event.
  /// </summary>
  /// <param name="sender">The sender of the event.</param>
  /// <param name="e">The event args.</param>
  private void ILoggerRepository_Shutdown(object sender, EventArgs e)
  {
    if (sender is ILoggerRepository repository)
    {
      // Remove all repository from map
      RepositoryShutdown(repository);
    }
  }

  /// <summary>
  /// The handler to use to create the extension wrapper objects.
  /// </summary>
  private readonly WrapperCreationHandler _createWrapperHandler;

  /// <summary>
  /// Internal reference to the delegate used to register for repository shutdown events.
  /// </summary>
  private readonly LoggerRepositoryShutdownEventHandler _shutdownHandler;
}
