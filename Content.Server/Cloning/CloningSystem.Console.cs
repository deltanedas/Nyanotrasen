using JetBrains.Annotations;
using Robust.Shared.Timing;
using Content.Server.Medical.Components;
using Content.Server.Cloning.Components;
using Content.Server.Power.Components;
using Content.Server.Mind.Components;
using Content.Server.MachineLinking.System;
using Content.Server.MachineLinking.Events;
using Content.Server.UserInterface;
using Content.Shared.MobState.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Content.Shared.Cloning.CloningConsole;
using Content.Shared.Cloning;
using Content.Shared.MachineLinking.Events;

namespace Content.Server.Cloning.Systems
{
    [UsedImplicitly]
    public sealed partial class CloningConsoleSystem : EntitySystem
    {
        [Dependency] private readonly SignalLinkerSystem _signalSystem = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly CloningSystem _cloningSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CloningConsoleComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<CloningConsoleComponent, UiButtonPressedMessage>(OnButtonPressed);
            SubscribeLocalEvent<CloningConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpen);
            SubscribeLocalEvent<CloningConsoleComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<CloningConsoleComponent, NewLinkEvent>(OnNewLink);
            SubscribeLocalEvent<CloningConsoleComponent, PortDisconnectedEvent>(OnPortDisconnected);
            SubscribeLocalEvent<CloningPodComponent, PortDisconnectedEvent>(OnPodPortDisconnected);
            SubscribeLocalEvent<MedicalScannerComponent, PortDisconnectedEvent>(OnScannerPortDisconnected);
        }

        private void OnInit(EntityUid uid, CloningConsoleComponent component, ComponentInit args)
        {
            _signalSystem.EnsureTransmitterPorts(uid, component.ScannerPort, component.PodPort);
        }
        private void OnButtonPressed(EntityUid uid, CloningConsoleComponent consoleComponent, UiButtonPressedMessage args)
        {
            if (!consoleComponent.Powered)
                return;

            switch (args.Button)
            {
                case UiButton.Clone:
                    if (consoleComponent.GeneticScanner != null && consoleComponent.CloningPod != null)
                        TryClone(uid, consoleComponent.CloningPod.Value, consoleComponent.GeneticScanner.Value, consoleComponent: consoleComponent);
                    break;
                case UiButton.Eject:
                    if (consoleComponent.CloningPod != null)
                        TryEject(uid, consoleComponent.CloningPod.Value, consoleComponent: consoleComponent);
                    break;
            }
            UpdateUserInterface(consoleComponent);
        }

        private void OnPowerChanged(EntityUid uid, CloningConsoleComponent component, PowerChangedEvent args)
        {
            component.Powered = args.Powered;
            UpdateUserInterface(component);
        }

        private void OnNewLink(EntityUid uid, CloningConsoleComponent component, NewLinkEvent args)
        {
            Logger.Error("args.TransmitterPort is " + args.TransmitterPort);
            Logger.Error("args.Receiver is: " + args.Receiver);
            if (TryComp<MedicalScannerComponent>(args.Receiver, out var scanner) && args.TransmitterPort == "MedicalScannerSender")
            {
                Logger.Error("Adding scanner...");
                component.GeneticScanner = args.Receiver;
                scanner.ConnectedConsole = uid;
            }

            if (TryComp<CloningPodComponent>(args.Receiver, out var pod) && args.TransmitterPort == "CloningPodSender")
            {
                Logger.Error("Adding pod...");
                component.CloningPod = args.Receiver;
                pod.ConnectedConsole = uid;
            }
            UpdateUserInterface(component);
        }

        private void OnPortDisconnected(EntityUid uid, CloningConsoleComponent component, PortDisconnectedEvent args)
        {
            Logger.Error("disconnected port is... " + args.Port);
            if (args.Port == "MedicalScannerSender")
                component.GeneticScanner = null;

            if (args.Port == "CloningPodSender")
                component.CloningPod = null;

            UpdateUserInterface(component);
        }

        private void OnPodPortDisconnected(EntityUid uid, CloningPodComponent pod, PortDisconnectedEvent args)
        {
            pod.ConnectedConsole = null;
        }

        private void OnScannerPortDisconnected(EntityUid uid, MedicalScannerComponent component, PortDisconnectedEvent args)
        {
            component.ConnectedConsole = null;
        }

        private void OnUIOpen(EntityUid uid, CloningConsoleComponent component, AfterActivatableUIOpenEvent args)
        {
            UpdateUserInterface(component);
        }

        private void UpdateUserInterface(CloningConsoleComponent consoleComponent)
        {
            if (!consoleComponent.Powered)
            {
                _uiSystem.GetUiOrNull(consoleComponent.Owner, CloningConsoleUiKey.Key)?.CloseAll();
                return;
            }

            var newState = GetUserInterfaceState(consoleComponent);

            _uiSystem.GetUiOrNull(consoleComponent.Owner, CloningConsoleUiKey.Key)?.SetState(newState);
        }

        public void TryEject(EntityUid uid, EntityUid clonePodUid, CloningPodComponent? cloningPod = null, CloningConsoleComponent? consoleComponent = null)
        {
            if (!Resolve(uid, ref consoleComponent) || !Resolve(clonePodUid, ref cloningPod))
                return;

            _cloningSystem.Eject(clonePodUid, cloningPod);
        }

        public void TryClone(EntityUid uid, EntityUid cloningPodUid, EntityUid scannerUid, CloningPodComponent? cloningPod = null, MedicalScannerComponent? scannerComp = null, CloningConsoleComponent? consoleComponent = null)
        {
            if (!Resolve(uid, ref consoleComponent) || !Resolve(cloningPodUid, ref cloningPod)  || !Resolve(scannerUid, ref scannerComp))
                return;

            if (scannerComp.BodyContainer.ContainedEntity is null)
                return;

            if (!TryComp<MindComponent>(scannerComp.BodyContainer.ContainedEntity.Value, out var mindComp))
                return;

            var mind = mindComp.Mind;

            if (mind == null || mind.UserId.HasValue == false || mind.Session == null)
                return;

            bool cloningSuccessful = _cloningSystem.TryCloning(cloningPodUid, scannerComp.BodyContainer.ContainedEntity.Value, mind, cloningPod);
        }
        private CloningConsoleBoundUserInterfaceState GetUserInterfaceState(CloningConsoleComponent consoleComponent)
        {
            ClonerStatus clonerStatus = ClonerStatus.Ready;

            // genetic scanner info
            string scanBodyInfo = "Unknown";
            bool scannerConnected = false;
            bool scannerInRange = false;
            if (consoleComponent.GeneticScanner != null && TryComp<MedicalScannerComponent>(consoleComponent.GeneticScanner, out var scanner)) {

                scannerConnected = true;
                EntityUid? scanBody = scanner.BodyContainer.ContainedEntity;

                Transform(scanner.Owner).Coordinates.TryDistance(EntityManager, Transform((EntityUid) consoleComponent.Owner).Coordinates, out float distance);
                scannerInRange = (distance <= consoleComponent.MaxDistance);

                // GET NAME
                if (TryComp<MetaDataComponent>(scanBody, out var scanMetaData))
                    scanBodyInfo = scanMetaData.EntityName;

                // GET STATE
                if (scanBody == null)
                    clonerStatus = ClonerStatus.ScannerEmpty;
                else if (TryComp<MobStateComponent>(scanBody, out var mobState))
                {
                    TryComp<MindComponent>(scanBody, out var mindComp);

                    if (!mobState.IsDead())
                    {
                        clonerStatus = ClonerStatus.ScannerOccupantAlive;
                    }
                    else
                    {
                        if (mindComp == null || mindComp.Mind == null || mindComp.Mind.UserId == null || !_playerManager.TryGetSessionById(mindComp.Mind.UserId.Value, out var client))
                        {
                            clonerStatus = ClonerStatus.NoMindDetected;
                        }
                    }
                }
            }

            // cloning pod info
            var cloneBodyInfo = "Unknown";
            float cloningProgress = 0;
            float cloningTime = 30f;
            bool clonerProgressing = false;
            bool clonerConnected = false;
            bool clonerMindPresent = false;
            bool clonerInRange = false;
            if (consoleComponent.CloningPod != null && TryComp<CloningPodComponent>(consoleComponent.CloningPod, out var clonePod))
            {
                clonerConnected = true;
                EntityUid? cloneBody = clonePod.BodyContainer.ContainedEntity;
                if (TryComp<MetaDataComponent>(cloneBody, out var cloneMetaData))
                    cloneBodyInfo = cloneMetaData.EntityName;

                Transform(clonePod.Owner).Coordinates.TryDistance(EntityManager, Transform((EntityUid) consoleComponent.Owner).Coordinates, out float distance);
                clonerInRange = (distance <= consoleComponent.MaxDistance);

                cloningProgress = clonePod.CloningProgress;
                cloningTime = clonePod.CloningTime;
                clonerProgressing = _cloningSystem.IsPowered(clonePod) && (clonePod.BodyContainer.ContainedEntity != null);
                clonerMindPresent = clonePod.Status == CloningPodStatus.Cloning;
                if (cloneBody != null)
                {
                    clonerStatus = ClonerStatus.ClonerOccupied;
                }
            }
            else
            {
                clonerStatus = ClonerStatus.NoClonerDetected;
            }

            return new CloningConsoleBoundUserInterfaceState(
                scanBodyInfo,
                cloneBodyInfo,
                _gameTiming.CurTime,
                cloningProgress,
                cloningTime,
                clonerProgressing,
                clonerMindPresent,
                clonerStatus,
                scannerConnected,
                scannerInRange,
                clonerConnected,
                clonerInRange
                );
        }
    }
}
