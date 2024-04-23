﻿angular.module('umbraco').controller('Umbraco.Community.BlockPreview.Controllers.BlockPreviewController',
    ['$scope', '$sce', '$timeout', 'editorState', 'Umbraco.Community.BlockPreview.Resources.PreviewResource',
        function ($scope, $sce, $timeout, editorState, previewResource) {
            var current = editorState.getCurrent();
            var active = current.variants.find(function (v) {
                return v.active;
            });

            if (active !== null) {
                if (active.language !== null) {
                    $scope.language = active.language.culture;
                }
            }

            $scope.cacheBuster = Umbraco.Sys.ServerVariables.application.cacheBuster;

            $scope.id = current.id;
            $scope.loading = true;
            $scope.markup = '<div class="preview-alert preview-alert-info">Loading preview</div>';

            $scope.contentTypeAlias = current.contentTypeAlias;

            // There must be a better way to do this...
            $scope.dataTypeKey = '';
            var parent = $scope.$parent;

            while (parent.$parent) {
                if (parent.vm) {
                    if (parent.vm.constructor.name == 'BlockGridController') {
                        $scope.dataTypeKey = parent.vm.model.dataTypeKey;
                        break;
                    }
                }

                parent = parent.$parent;
            }

            function loadPreview(content, settings) {
                $scope.markup = $sce.trustAsHtml('<div class="preview-alert preview-alert-info">Loading preview</div>');
                $scope.loading = true;

                var formattedBlockData = {
                    layout: $scope.block.layout,
                    contentData: [content || $scope.block.data],
                    settingsData: [settings || $scope.block.settingsData]
                };

                previewResource.getPreview(formattedBlockData, $scope.id, $scope.dataTypeKey, $scope.model.constructor.name == 'BlockGridBlockController', $scope.language).then(function (data) {
                    $scope.markup = $sce.trustAsHtml(data);
                    $scope.loading = false;
                });
            }

            loadPreview($scope.block.data, $scope.block.settingsData);

            var timeoutPromise;

            $scope.$watch('block.data', function (newValue, oldValue) {
                if (newValue !== oldValue) {
                    $timeout.cancel(timeoutPromise);

                    timeoutPromise = $timeout(function () {   //Set timeout
                        loadPreview(newValue, null);
                    }, 500);
                }
            }, true);

            $scope.$watch('block.settingsData', function (newValue, oldValue) {
                if (newValue !== oldValue) {
                    $timeout.cancel(timeoutPromise);

                    timeoutPromise = $timeout(function () {   //Set timeout
                        loadPreview(null, newValue);
                    }, 500);
                }
            }, true);

            $scope.editBlock = function ($event, block) {
                var target = $event.target;
                var blockActions = target.closest('.umb-block-grid__block--actions');
                var areaCreate = target.closest('.umb-block-grid__create-button');
                var blockCreateButton = target.closest('.umb-block-grid__block--inline-create-button');
                var blockCreateButtonLast = target.closest('.umb-block-grid__block--last-inline-create-button');
                var blockScaling = target.closest('.umb-block-grid__scale-handler') || target.closest('.--scale-mode');

                if (!blockActions && !areaCreate && !blockCreateButton && !blockCreateButtonLast && !blockScaling) {
                    block.edit();
                    $event.preventDefault();
                    $event.stopPropagation();
                    $event.stopImmediatePropagation();
                    $event.cancelBubble = true;
                    return;
                }
            }
        }
    ]);
