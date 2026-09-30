# DSCP Registry and Identity Mapping in OptiRoute

## 1. Core principle: DSCP as application identity

In OptiRoute, the **DSCP** (Differentiated Services Code Point) field is used as a **unique, network-wide identifier of the executable process**, not as a fixed representation of an egress gateway.

```
bf6.exe     -> DSCP 33 (network-wide)
discord.exe -> DSCP 34 (network-wide)
steam.exe   -> DSCP 35 (network-wide)
```

This model has three benefits:

1. Multi-PC without collision. If two PCs on the same network both run `bf6.exe`, both mark their packets with DSCP 33. OPNsense can apply the global route to both, or apply a per-host override based on `Source IP`.
2. Route independence. Switching the game's route from `WAN1` to `WAN2` on OPNsense does not require recreating the Windows QoS policy or changing the application's DSCP. The process identity stays stable.
3. Scalability. Firewall rules become expressive: the DSCP identifies the application, and `Source IP` distinguishes clients.

## 2. Managed DSCP pool

DSCP is a 6-bit field (values from `0` to `63`). OptiRoute allocates values dynamically through the `DscpRegistry` class, which maintains a safe pool and prevents collisions with standard network traffic.

### 2.1 Reserved values (excluded from the pool)

- `DSCP 0` (Best Effort / CS0): ordinary internet traffic without policy.
- `DSCP 46` (Expedited Forwarding, EF): reserved for latency-sensitive traffic such as VoIP.
- Standard IETF classes:
  - CS1 through CS7 (`8, 16, 24, 32, 40, 48, 56`)
  - AF1x through AF4x (`10, 12, 14, 18, 20, 22, 26, 28, 30, 34, 36, 38`)

### 2.2 Default OptiRoute pool

OptiRoute prefers non-conflicting values:

```
Pool = [33, 35, 37, 39, 41, 42, 43, 44, 45, 47, 49, 50, 51, ..., 62]
```

## 3. Mathematical relationship between DSCP and ToS

In the IPv4 header, the 6 most significant bits of the ToS byte hold the DSCP, and the 2 least significant bits are reserved for ECN (Explicit Congestion Notification):

```
ToS byte = DSCP << 2 = DSCP * 4
```

### Quick reference table

| Executable (example) | DSCP (decimal) | DSCP (binary) | ToS byte (hex) | Wireshark / pcap filter |
| --- | --- | --- | --- | --- |
| default traffic | 0 | `000000` | `0x00` | `ip.dsfield.dscp == 0` |
| `bf6.exe` | 33 | `100001` | `0x84` | `ip.dsfield.dscp == 33` |
| `discord.exe` | 34 | `100010` | `0x88` | `ip.dsfield.dscp == 34` |
| `steam.exe` | 35 | `100011` | `0x8C` | `ip.dsfield.dscp == 35` |
| `valorant.exe` | 37 | `100101` | `0x94` | `ip.dsfield.dscp == 37` |

## 4. Conflict resolution

When one machine has a legacy Windows policy that marks `bf6.exe` with DSCP 33, but another user on the network registered `bf6.exe` on OPNsense as DSCP 35:

1. `OptiRouteSynchronizer` detects the `ApplicationSyncState.Conflict` state.
2. The UI highlights the card in red with the message `DSCP mismatch detected`.
3. The user clicks `Repair to global DSCP`, which automatically reconfigures the local Windows policy to the official DSCP registered on the firewall (`35`) and restores consistency across the network.