from cryptography.hazmat.primitives.asymmetric import dh
from cryptography.hazmat.primitives import serialization
from Crypto.Cipher import AES
from Crypto.Util.Padding import pad, unpad
import socket
import threading
import sys
import json
import hashlib
import queue
import argparse
from flask import Flask, Response, request, jsonify, render_template

# Safe UTF-8 configuration for Windows standard streams
if sys.platform.startswith('win'):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

# Parse command line arguments
parser = argparse.ArgumentParser(description="DH-AES Secure Chat Client")
parser.add_argument("--port", type=int, default=5002, help="Flask server port")
parser.add_argument("--username", type=str, default="Bob", help="Chat username")
args = parser.parse_args()

username = args.username
flask_port = args.port
socket_port = 12347

# Socket variables
client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
aes_key = None
encryption_info = {} # Information displayed in Security Panel

# Flask app configuration
app = Flask(__name__)
message_history = []
sse_queues = []
online_users = []
online_users_lock = threading.Lock()

def send_msg(sock, data):
    length = len(data)
    sock.sendall(length.to_bytes(4, 'big') + data)

def recv_msg(sock):
    try:
        raw_length = sock.recv(4)
        if not raw_length:
            return None
        length = int.from_bytes(raw_length, 'big')
        data = bytearray()
        while len(data) < length:
            packet = sock.recv(length - len(data))
            if not packet:
                return None
            data.extend(packet)
        return bytes(data)
    except Exception:
        return None

def encrypt_aes(key, plaintext_bytes):
    cipher = AES.new(key, AES.MODE_CBC)
    ciphertext = cipher.encrypt(pad(plaintext_bytes, AES.block_size))
    return cipher.iv + ciphertext

def decrypt_aes(key, ciphertext_with_iv):
    iv = ciphertext_with_iv[:16]
    ciphertext = ciphertext_with_iv[16:]
    cipher = AES.new(key, AES.MODE_CBC, iv)
    return unpad(cipher.decrypt(ciphertext), AES.block_size)

# Connect to DH Socket Server and perform DH Key Agreement
def connect_socket_server():
    global aes_key, encryption_info
    print(f"[Client] Connecting to DH Socket Server at localhost:{socket_port}...")
    try:
        client_socket.connect(('localhost', socket_port))
        print("[Client] Connected to server.")

        # Step 1: Receive DH parameters & server public key
        handshake_data_bytes = recv_msg(client_socket)
        if not handshake_data_bytes:
            print("[Client] Disconnected during handshaking.")
            sys.exit(1)
        
        handshake_data = json.loads(handshake_data_bytes.decode('utf-8'))
        param_pem = handshake_data.get("param_pem")
        server_pub_pem = handshake_data.get("server_pub_pem")

        # Load DH parameters and server public key
        parameters = serialization.load_pem_parameters(param_pem.encode('utf-8'))
        server_public_key = serialization.load_pem_public_key(server_pub_pem.encode('utf-8'))

        # Step 2: Generate Client DH private & public keys
        print("[Client] Generating DH key pair...")
        client_private_key = parameters.generate_private_key()
        client_public_key = client_private_key.public_key()
        client_pub_pem = client_public_key.public_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PublicFormat.SubjectPublicKeyInfo
        ).decode('utf-8')

        # Step 3: Compute Shared Secret & Derive AES Session Key
        shared_secret = client_private_key.exchange(server_public_key)
        # Derive key via KDF (SHA-256 hash of the shared secret)
        aes_key = hashlib.sha256(shared_secret).digest()[:16]

        print("[Client] DH Key Agreement completed.")
        print(f"Negotiated AES Key (Hex): {aes_key.hex()}")

        # Store DH params and keys for display in Security Panel
        p_val = parameters.parameter_numbers().p
        g_val = parameters.parameter_numbers().g

        encryption_info = {
            "algorithm": "Diffie-Hellman + AES",
            "p_hex": hex(p_val),
            "g_val": str(g_val),
            "server_pub_key": server_pub_pem,
            "client_pub_key": client_pub_pem,
            "shared_secret_hex": shared_secret.hex(),
            "aes_key_hex": aes_key.hex()
        }

        # Step 4: Send client public key to the server
        client_pub_data = {
            "client_pub_pem": client_pub_pem
        }
        send_msg(client_socket, json.dumps(client_pub_data).encode('utf-8'))

        # Step 5: Send encrypted username for registration
        reg_data = {"username": username}
        reg_encrypted = encrypt_aes(aes_key, json.dumps(reg_data).encode('utf-8'))
        send_msg(client_socket, reg_encrypted)

        # Launch background socket receiver thread
        t = threading.Thread(target=socket_receiver)
        t.daemon = True
        t.start()

    except ConnectionRefusedError:
        print("[Client] Error: DH Socket server is not running or connection refused.")
        sys.exit(1)
    except Exception as e:
        print(f"[Client] Error during handshaking: {e}")
        sys.exit(1)

def socket_receiver():
    global online_users
    while True:
        try:
            encrypted_payload = recv_msg(client_socket)
            if not encrypted_payload:
                print("[Client] Disconnected from Socket Server.")
                notify_web({"type": "system", "content": "Mất kết nối với Socket Server."})
                break

            # Decrypt the payload
            decrypted_bytes = decrypt_aes(aes_key, encrypted_payload)
            msg_data = json.loads(decrypted_bytes.decode('utf-8'))

            if msg_data.get("type") == "user_list":
                with online_users_lock:
                    online_users = msg_data.get("users", [])
                notify_web(msg_data)

            elif msg_data.get("type") == "system":
                notify_web(msg_data)

            elif msg_data.get("type") == "message":
                sender = msg_data.get("sender")
                content = msg_data.get("content")
                client_hash = msg_data.get("hash")
                raw_hex = msg_data.get("raw_hex", encrypted_payload.hex())

                # Verify MD5 integrity
                computed_hash = hashlib.md5(content.encode('utf-8')).hexdigest()
                is_valid = (computed_hash == client_hash)

                formatted_msg = {
                    "type": "message",
                    "sender": sender,
                    "content": content,
                    "hash": client_hash,
                    "hash_type": "MD5",
                    "integrity_valid": is_valid,
                    "raw_hex": raw_hex
                }
                
                # Append to history and push to connected browsers
                message_history.append(formatted_msg)
                notify_web(formatted_msg)

        except Exception as e:
            print(f"[Client] Error in socket receiver: {e}")
            break

def notify_web(data):
    for q in list(sse_queues):
        try:
            q.put_nowait(data)
        except Exception:
            pass

# Flask Route - Web Interface
@app.route('/')
def index():
    return render_template('index.html', username=username, encryption_info=encryption_info)

# Flask Route - Server-Sent Events for live updates
@app.route('/events')
def events():
    def event_stream():
        q = queue.Queue()
        sse_queues.append(q)
        try:
            yield f"data: {json.dumps({'type': 'connection_status', 'status': 'connected', 'username': username, 'encryption_info': encryption_info, 'online_users': online_users})}\n\n"
            for msg in message_history:
                yield f"data: {json.dumps(msg)}\n\n"
            while True:
                msg = q.get()
                yield f"data: {json.dumps(msg)}\n\n"
        except GeneratorExit:
            sse_queues.remove(q)
    return Response(event_stream(), mimetype="text/event-stream")

# Flask Route - Send Message from Browser
@app.route('/send', methods=['POST'])
def send_message_api():
    content = request.json.get('content')
    if not content:
        return jsonify({"status": "error", "message": "Empty message"}), 400

    try:
        # Step 1: Calculate MD5 hash for message integrity
        msg_hash = hashlib.md5(content.encode('utf-8')).hexdigest()

        # Step 2: Construct JSON structure
        payload_data = {
            "type": "message",
            "content": content,
            "hash": msg_hash
        }

        # Step 3: Encrypt the payload with the AES key
        encrypted_payload = encrypt_aes(aes_key, json.dumps(payload_data).encode('utf-8'))

        # Step 4: Send the encrypted bytes over TCP socket
        send_msg(client_socket, encrypted_payload)
        return jsonify({"status": "ok"})
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

if __name__ == "__main__":
    # Perform DH Key exchange with server first
    connect_socket_server()

    # Disable Flask output logging
    import logging
    log = logging.getLogger('werkzeug')
    log.setLevel(logging.ERROR)

    print(f"\n[Client] Web interface is running at: http://localhost:{flask_port}")
    print(f"[Client] Opening chat room as {username}...")
    
    # Run Flask local web service
    app.run(host='localhost', port=flask_port, debug=False, threaded=True)
