# Rokuro AI Agent Guide

- Unity project built with **Unity 6000.4.2f1**; open the repo in that editor version before changing code.
- Default build scene is `Assets/Projects/Scenes/SampleScene.unity` (`ProjectSettings/EditorBuildSettings.asset`); also check `Assets/Projects/Scenes/Main Scene.unity` and `Assets/Projects/Scenes/Blacksmith's Forge.unity` for live wiring.
- Big-picture runtime split:
  - `Assets/Features/Clay` = the main clay simulation feature.
  - `Assets/Projects/Scripts` = app-specific runtime glue for XR/controller input.
  - `Assets/vFolders` and `Assets/vHierarchy` = editor-only hierarchy/folder tooling.
- `ClayManager` is the orchestration point: it updates `ClayForce` → `ClayCompute` → rendering (`ClayRenderer`, `ClayParticleRenderer`, `ClayGridVelRenderer`) every frame.
- `ClayCompute` owns the GPU buffers and compute shader dispatch; its buffer sizes and `ObjectForce` struct layout must stay aligned with the HLSL side.
- `ClayRenderer` passes the particle buffer into `ClayRenderFeature.Setup(...).Forget()`; `ClayRenderFeature` is a URP `ScriptableRendererFeature` that waits for its pass before binding buffers/uniforms.
- When editing `Assets/Features/Clay`, keep shader/kernel/property names in sync with the enum wrappers in `ComputeShaderWrapper<TKernel,TUniform>` and `MaterialWrapper<TProp>`.
- `ClayForce` uses `Physics.OverlapSphereNonAlloc` and `Collider.attachedRigidbody`; it only activates kinematic rigidbodies, so force behavior is intentionally filtered.
- `ControllerManager` reads `InputActionReference` values each `Update()` and spawns left/right hand prefabs under itself; the scene references are in `Main Scene`, `SampleScene`, and `Blacksmith's Forge`.
- `Assets/Projects/Scripts/Inputs/XRIDefaultInputActions.cs` is auto-generated from `Assets/Samples/XR Interaction Toolkit/3.4.0/Starter Assets/XRI Default Input Actions.inputactions`; edit the `.inputactions` asset, not the generated C#.
- `Assets/vFolders` and `Assets/vHierarchy` are `#if UNITY_EDITOR` + editor-only asmdefs; they use reflection against Unity internal tree view APIs and have Unity-version-specific branches (`UNITY_6000_2_OR_NEWER`, `UNITY_6000_3_OR_NEWER`).
- `Assets/VIVEOpenXRInstaller.cs` is editor menu tooling under `VIVE/OpenXR Installer/*` for Git-based package install/update.
- Dependencies worth remembering: `com.cysharp.unitask`, `com.unity.inputsystem`, `com.unity.render-pipelines.universal`, `com.unity.xr.openxr`, `com.unity.xr.oculus`, `com.htc.upm.vive.openxr`, Odin Inspector, vFolders, and vHierarchy.
- There are no repo-local test instructions or dedicated test assemblies discovered; the practical validation path is Unity compile + Console + Play Mode in the affected scene.
- For editor-only changes, re-open/compile in Unity after touching `Assets/vFolders`, `Assets/vHierarchy`, or `Assets/VIVEOpenXRInstaller.cs` because those files depend on editor APIs and reflection-heavy internals.
- Preserve the existing style: older runtime scripts in `Assets/Projects/Scripts` are mostly namespace-less, while feature code under `Assets/Features/*` uses namespaces like `Features.Clay.Scripts`.

