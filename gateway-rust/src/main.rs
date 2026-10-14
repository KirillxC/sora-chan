use std::net::SocketAddr;
use std::sync::Arc;
use tokio::net::UdpSocket;
use tokio::sync::mpsc;
use byteorder::{BigEndian, ReadBytesExt};
use std::io::Cursor;
use log::{info, error, warn};

// Binary Packet Header Magic: 0x41 0x49 (ASCII 'A', 'I')
const PACKET_MAGIC: u16 = 0x4149;

#[derive(Debug)]
pub enum InboundPacket {
    Telemetry {
        timestamp_ms: u64,
        head_pos: [f32; 3],
        head_rot: [f32; 4],
        gaze_dir: [f32; 3],
        ball_pos: [f32; 3],
        ball_vel: [f32; 3],
    },
    TouchEvent {
        zone_id: u8,
        gesture_id: u8,
        intensity: f32,
        duration_sec: f32,
    },
    Heartbeat,
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info")).init();

    let listen_addr = "0.0.0.0:9001";
    let socket = Arc::new(UdpSocket::bind(listen_addr).await?);
    info!("[Gateway-Rust] Listening for Unity binary telemetry on {}", listen_addr);

    let (tx, mut rx) = mpsc::channel::<InboundPacket>(1024);

    // Receiver loop
    let recv_socket = Arc::clone(&socket);
    tokio::spawn(async move {
        let mut buf = [0u8; 2048];
        loop {
            match recv_socket.recv_from(&mut buf).await {
                Ok((size, src)) => {
                    if let Some(packet) = parse_packet(&buf[..size]) {
                        if let Err(e) = tx.send(packet).await {
                            error!("Channel send error: {}", e);
                            break;
                        }
                    }
                }
                Err(e) => {
                    error!("UDP recv error: {}", e);
                }
            }
        }
    });

    // Event router to Python Brain Host
    while let Some(packet) = rx.recv().await {
        match packet {
            InboundPacket::TouchEvent { zone_id, gesture_id, intensity, duration_sec } => {
                info!("[Touch] Zone: {}, Gesture: {}, Intensity: {:.2}, Dur: {:.2}s",
                    zone_id, gesture_id, intensity, duration_sec);
            }
            InboundPacket::Telemetry { head_pos, gaze_dir, ball_pos, .. } => {
                // High frequency tick (90Hz) - forwarded to state aggregator
            }
            InboundPacket::Heartbeat => {}
        }
    }

    Ok(())
}

fn parse_packet(data: &[u8]) -> Option<InboundPacket> {
    if data.len() < 4 {
        return None;
    }
    let mut rdr = Cursor::new(data);
    let magic = rdr.read_u16::<BigEndian>().ok()?;
    if magic != PACKET_MAGIC {
        return None;
    }
    let packet_type = rdr.read_u8().ok()?;

    match packet_type {
        1 => {
            // Telemetry
            if data.len() < 4 + 8 + (3 + 4 + 3 + 3 + 3) * 4 {
                return None;
            }
            let ts = rdr.read_u64::<BigEndian>().ok()?;
            let hp = [rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?];
            let hr = [rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?];
            let gd = [rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?];
            let bp = [rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?];
            let bv = [rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?, rdr.read_f32::<BigEndian>().ok()?];

            Some(InboundPacket::Telemetry {
                timestamp_ms: ts,
                head_pos: hp,
                head_rot: hr,
                gaze_dir: gd,
                ball_pos: bp,
                ball_vel: bv,
            })
        }
        2 => {
            // Touch Event
            let zone = rdr.read_u8().ok()?;
            let gesture = rdr.read_u8().ok()?;
            let intensity = rdr.read_f32::<BigEndian>().ok()?;
            let duration = rdr.read_f32::<BigEndian>().ok()?;
            Some(InboundPacket::TouchEvent {
                zone_id: zone,
                gesture_id: gesture,
                intensity,
                duration_sec: duration,
            })
        }
        _ => None,
    }
}
