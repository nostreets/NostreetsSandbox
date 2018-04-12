﻿(function () {

    page.baseController = angular.module(page.APPNAME)
                                 .factory("$baseController", baseController);

    baseController.$inject = ['$document', '$systemEventService', '$alertService', "$window", '$uibModal', '$timeout', '$http', '$sce', '$cookies', '$q'];

    function baseController($document, $systemEventService, $alertService, $window, $uibModal, $timeout, $http, $sce, $cookies, $q) {

        //PUBLIC
        var base = {
            document: $document,
            event: $systemEventService,
            alert: $alertService,
            window: $window,
            modal: $uibModal,
            timeout: $timeout,
            http: $http,
            sce: $sce,
            cookies: $cookies,
            Q: $q
        }

        base.tryAgain = function (maxLoops, miliseconds, promiseMethod, onSuccess) {

            if (onSuccess === null) { onSuccess = (data) => console.log(data); }
            var root = {};

            root.promiseMethod = promiseMethod;
            root.currentIndex = 0;

            _start();

            function _start() {
                var method = () => _repeatUntilSuccessful(root.promiseMethod(), miliseconds, maxLoops, data => onSuccess(data));
                base.timeout(method, miliseconds);
            }

            function _repeatUntilSuccessful(promise, time, maxLoops, callback = null) {

                function delay(time, val) {
                    return new Promise(function (resolve) {
                        setTimeout(function () {
                            resolve(val);
                        }, time);
                    });
                }

                function success(data) {
                    if (callback !== null) {
                        callback(data);
                    }
                    return;
                }

                function error(data) {
                    if (root.currentIndex >= maxLoops) { return; }
                    else {
                        return delay(time).then(
                            () => { root.currentIndex++; _repeatUntilSuccessful(root.promiseMethod(), time, maxLoops, callback); },
                            () => { root.currentIndex++; _repeatUntilSuccessful(root.promiseMethod(), time, maxLoops, callback); }
                        );
                    }
                }

                base.Q.when(promise, success, error);

            }

        }

        base.errorCheck = function (err, tryAgainObj) {

            if (!tryAgainObj)
                tryAgainObj = {};


            if (!tryAgainObj.maxLoops)
                tryAgainObj.maxLoops = 3;


            if (!tryAgainObj.miliseconds)
                tryAgainObj.miliseconds = 1000;


            if (!tryAgainObj.promiseMethod)
                tryAgainObj.promiseMethod = () => {
                    return new Promise((resolve, reject) => {
                        resolve();
                    });
                }


            if (!tryAgainObj.onSuccess) 
                tryAgainObj.onSuccess = (data) => console.log(data);
            

            if (err.data.errors && err.data.errors.length) {
                for (var error of err.data.errors) {
                    switch (error) {

                        default:
                            if (!tryAgainObj || !tryAgainObj.maxLoops || !tryAgainObj.miliseconds || !tryAgainObj.promiseMethod)
                                return;
                            else
                                base.tryAgain(tryAgainObj.maxLoops
                                    , tryAgainObj.miliseconds
                                    , tryAgainObj.promiseMethod
                                    , tryAgainObj.onSuccess);
                            break;
                    }
                }
            }
            else {
                switch (err.status) {
                    //case 401:
                    //    base.loginPopup();
                    //    break;

                    default:
                        if (!tryAgainObj || !tryAgainObj.maxLoops || !tryAgainObj.miliseconds || !tryAgainObj.promiseMethod) { return; }
                        base.tryAgain(tryAgainObj.maxLoops
                            , tryAgainObj.miliseconds
                            , tryAgainObj.promiseMethod
                            , tryAgainObj.onSuccess);
                        break;
                }
            }
        }

        return base;
    }

})();