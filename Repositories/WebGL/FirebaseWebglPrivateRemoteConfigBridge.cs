#if UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;
using Modules.Firebase.Adapter.Impl;
using Newtonsoft.Json;

namespace Build1.UnityConfig.Repositories.WebGL
{
    internal sealed class FirebaseWebglPrivateRemoteConfigBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private const int RequestTimeoutMs = 60000;

        private delegate void EnsureModulesSuccessCallback(int requestId);
        private delegate void RequestCallback(int requestId, string value);
        private delegate void NativeCall(RequestCallback onSuccess, RequestCallback onError, int requestId);
#endif

        private readonly object _syncRoot = new object();
        private Task _ensureRemoteConfigModuleTask;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal", EntryPoint = "ensureFirebaseModules_Private")]
        private static extern void EnsureModulesNative(
            string moduleNamesCsv,
            EnsureModulesSuccessCallback onSuccess,
            RequestCallback onError,
            int requestId);

        [DllImport("__Internal", EntryPoint = "remoteConfigPrivateConfigure")]
        private static extern void ConfigureNative(
            string appName,
            int fetchTimeoutMillis,
            int minimumFetchIntervalMillis,
            RequestCallback onSuccess,
            RequestCallback onError,
            int requestId);

        [DllImport("__Internal", EntryPoint = "remoteConfigPrivateFetchAndActivate")]
        private static extern void FetchAndActivateNative(
            string appName,
            RequestCallback onSuccess,
            RequestCallback onError,
            int requestId);

        [DllImport("__Internal", EntryPoint = "remoteConfigPrivateGetValue")]
        private static extern void GetValueNative(
            string appName,
            string parameterName,
            RequestCallback onSuccess,
            RequestCallback onError,
            int requestId);

        [DllImport("__Internal", EntryPoint = "remoteConfigPrivateGetAll")]
        private static extern void GetAllNative(
            string appName,
            RequestCallback onSuccess,
            RequestCallback onError,
            int requestId);
#endif

        public Task Configure(int fetchTimeoutMillis, int minimumFetchIntervalMillis, string appName = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return RunRemoteConfigCall(
                "remoteConfigPrivateConfigure",
                (onSuccess, onError, requestId) => ConfigureNative(appName, fetchTimeoutMillis, minimumFetchIntervalMillis, onSuccess, onError, requestId));
#else
            throw new NotSupportedException("FirebaseWebglPrivateRemoteConfigBridge is WebGL runtime only.");
#endif
        }

        public Task<bool> FetchAndActivate(string appName = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FetchAndActivateInternal(appName);
#else
            throw new NotSupportedException("FirebaseWebglPrivateRemoteConfigBridge is WebGL runtime only.");
#endif
        }

        public Task<string> GetValue(string parameterName, string appName = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return RunRemoteConfigCallWithPayload(
                "remoteConfigPrivateGetValue",
                (onSuccess, onError, requestId) => GetValueNative(appName, parameterName, onSuccess, onError, requestId));
#else
            throw new NotSupportedException("FirebaseWebglPrivateRemoteConfigBridge is WebGL runtime only.");
#endif
        }

        public Task<Dictionary<string, string>> GetAll(string appName = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GetAllInternal(appName);
#else
            throw new NotSupportedException("FirebaseWebglPrivateRemoteConfigBridge is WebGL runtime only.");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private async Task RunRemoteConfigCall(string requestName, NativeCall nativeCall)
        {
            await EnsureRemoteConfigModuleLoaded();

            var request = new FirebaseWebglCallbackRequest(requestName, RequestTimeoutMs);

            try
            {
                nativeCall(OnRequestSuccess, OnRequestError, request.RequestId);
            }
            catch
            {
                request.Abort();
                throw;
            }

            await request.Task;
        }

        private async Task<string> RunRemoteConfigCallWithPayload(string requestName, NativeCall nativeCall)
        {
            await EnsureRemoteConfigModuleLoaded();

            var request = new FirebaseWebglCallbackRequest(requestName, RequestTimeoutMs);

            try
            {
                nativeCall(OnRequestSuccess, OnRequestError, request.RequestId);
            }
            catch
            {
                request.Abort();
                throw;
            }

            return await request.Task;
        }

        private async Task<bool> FetchAndActivateInternal(string appName)
        {
            var payload = await RunRemoteConfigCallWithPayload(
                "remoteConfigPrivateFetchAndActivate",
                (onSuccess, onError, requestId) => FetchAndActivateNative(appName, onSuccess, onError, requestId));
            return string.Equals(payload, "true", StringComparison.Ordinal);
        }

        private async Task<Dictionary<string, string>> GetAllInternal(string appName)
        {
            var payload = await RunRemoteConfigCallWithPayload(
                "remoteConfigPrivateGetAll",
                (onSuccess, onError, requestId) => GetAllNative(appName, onSuccess, onError, requestId));

            if (string.IsNullOrEmpty(payload))
                return new Dictionary<string, string>();

            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(payload);
            return values ?? new Dictionary<string, string>();
        }

        private Task EnsureRemoteConfigModuleLoaded()
        {
            lock (_syncRoot)
                return _ensureRemoteConfigModuleTask ??= EnsureRemoteConfigModuleLoadedInternal();
        }

        private static async Task EnsureRemoteConfigModuleLoadedInternal()
        {
            var request = new FirebaseWebglCallbackRequest("ensureFirebaseModules_Private(remoteConfig)", RequestTimeoutMs);

            try
            {
                EnsureModulesNative("remoteConfig", OnEnsureModulesSuccess, OnEnsureModulesError, request.RequestId);
            }
            catch
            {
                request.Abort();
                throw;
            }

            await request.Task;
        }

        [MonoPInvokeCallback(typeof(EnsureModulesSuccessCallback))]
        private static void OnEnsureModulesSuccess(int requestId)
        {
            if (!FirebaseWebglCallbackRequest.TryResolve(requestId, out var request))
                return;

            request.CompleteSuccess(string.Empty);
        }

        [MonoPInvokeCallback(typeof(RequestCallback))]
        private static void OnEnsureModulesError(int requestId, string error)
        {
            if (!FirebaseWebglCallbackRequest.TryResolve(requestId, out var request))
                return;

            request.CompleteError(new Exception(error));
        }

        [MonoPInvokeCallback(typeof(RequestCallback))]
        private static void OnRequestSuccess(int requestId, string payload)
        {
            if (!FirebaseWebglCallbackRequest.TryResolve(requestId, out var request))
                return;

            request.CompleteSuccess(payload);
        }

        [MonoPInvokeCallback(typeof(RequestCallback))]
        private static void OnRequestError(int requestId, string error)
        {
            if (!FirebaseWebglCallbackRequest.TryResolve(requestId, out var request))
                return;

            request.CompleteError(new Exception(error));
        }
#endif
    }
}

#endif
