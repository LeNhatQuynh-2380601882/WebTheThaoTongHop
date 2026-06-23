from cryptography.hazmat.primitives.asymmetric import dh
from cryptography.hazmat.primitives import serialization
from Crypto.Cipher import AES
from Crypto.Util.Padding import pad, unpad
import socket
import threading
import sys
import json
import hashlib

# Safe UTF-8 configuration for Windows standard streams
if sys.platform.startswith('win'):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

PORT = 12347
server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server_socket.bind(('localhost', PORT))
server_socket.listen(10)

print(f"[Server] Generating DH parameters (1024-bit for speed)...")
# Generate DH parameters once for the server
parameters = dh.generate_parameters(generator=2, key_size=1024)
param_pem = parameters.parameter_bytes(
    encoding=serialization.Encoding.PEM,
    format=serialization.ParameterFormat.PKCS3
).decode('utf-8')

print(f"[Server] Listening on localhost:{PORT}...")

clients = {}  # socket -> {username, aes_key, address}
clients_lock = threading.Lock()

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

def broadcast_message(message_dict, sender_sock=None):
    with clients_lock:
        active_clients = list(clients.items())

    msg_bytes = json.dumps(message_dict).encode('utf-8')

    for sock, info in active_clients:
        try:
            encrypted_payload = encrypt_aes(info['aes_key'], msg_bytes)
            send_msg(sock, encrypted_payload)
        except Exception as e:
            print(f"[Server] Error broadcasting to {info['username']}: {e}")

def handle_client(client_socket, client_address):
    print(f"[Server] Connection established with {client_address}")
    aes_key = None
    username = "Unknown"

    try:
        # Step 1: Server generates DH keys
        server_private_key = parameters.generate_private_key()
        server_public_key = server_private_key.public_key()
        server_pub_pem = server_public_key.public_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PublicFormat.SubjectPublicKeyInfo
        ).decode('utf-8')

        # Step 2: Send DH parameters & server public key
        handshake_data = {
            "param_pem": param_pem,
            "server_pub_pem": server_pub_pem
        }
        send_msg(client_socket, json.dumps(handshake_data).encode('utf-8'))

        # Step 3: Receive client public key
        client_pub_data_bytes = recv_msg(client_socket)
        if not client_pub_data_bytes:
            client_socket.close()
            return
        
        client_pub_data = json.loads(client_pub_data_bytes.decode('utf-8'))
        client_pub_pem = client_pub_data.get("client_pub_pem")
        client_public_key = serialization.load_pem_public_key(client_pub_pem.encode('utf-8'))

        # Step 4: Compute Shared Secret & AES Key
        shared_secret = server_private_key.exchange(client_public_key)
        # Derive a 16-byte AES key using SHA-256 hash of the shared secret
        aes_key = hashlib.sha256(shared_secret).digest()[:16]
        
        print(f"[Server] DH Key Exchange success. Exchanged Key (Hex): {aes_key.hex()}")

        # Step 5: Receive client registration (encrypted AES)
        reg_encrypted = recv_msg(client_socket)
        if not reg_encrypted:
            client_socket.close()
            return
        
        reg_bytes = decrypt_aes(aes_key, reg_encrypted)
        reg_data = json.loads(reg_bytes.decode('utf-8'))
        username = reg_data.get("username", f"User_{client_address[1]}")

        # Register client
        with clients_lock:
            clients[client_socket] = {
                'username': username,
                'aes_key': aes_key,
                'address': client_address
            }

        print(f"[Server] {username} ({client_address}) registered successfully.")

        # Broadcast join
        join_msg = {
            "type": "system",
            "content": f"{username} đã tham gia phòng chat."
        }
        broadcast_message(join_msg)

        # Broadcast list of users
        with clients_lock:
            online_users = [c['username'] for c in clients.values()]
        user_list_msg = {
            "type": "user_list",
            "users": online_users
        }
        broadcast_message(user_list_msg)

        # Handle client messages
        while True:
            encrypted_payload = recv_msg(client_socket)
            if not encrypted_payload:
                break

            decrypted_bytes = decrypt_aes(aes_key, encrypted_payload)
            msg_data = json.loads(decrypted_bytes.decode('utf-8'))

            if msg_data.get("type") == "message":
                content = msg_data.get("content")
                client_hash = msg_data.get("hash")

                # Verify MD5 hash for integrity check
                server_hash = hashlib.md5(content.encode('utf-8')).hexdigest()
                is_valid = (server_hash == client_hash)

                print(f"[Server] Message from {username}: '{content}' | MD5 Match: {is_valid}")

                # Broadcast to everyone
                broadcast_data = {
                    "type": "message",
                    "sender": username,
                    "content": content,
                    "hash": client_hash,
                    "hash_type": "MD5",
                    "integrity_valid": is_valid,
                    "raw_hex": encrypted_payload.hex()
                }
                broadcast_message(broadcast_data)
            
            elif msg_data.get("type") == "exit":
                break

    except Exception as e:
        print(f"[Server] Exception with client {username}: {e}")
    finally:
        with clients_lock:
            if client_socket in clients:
                del clients[client_socket]
        
        try:
            client_socket.close()
        except Exception:
            pass
        
        print(f"[Server] {username} disconnected.")
        
        # Broadcast leave message and updated user list
        leave_msg = {
            "type": "system",
            "content": f"{username} đã rời phòng chat."
        }
        broadcast_message(leave_msg)
        
        with clients_lock:
            online_users = [c['username'] for c in clients.values()]
        user_list_msg = {
            "type": "user_list",
            "users": online_users
        }
        broadcast_message(user_list_msg)

def accept_connections():
    while True:
        try:
            client_socket, client_address = server_socket.accept()
            t = threading.Thread(target=handle_client, args=(client_socket, client_address))
            t.daemon = True
            t.start()
        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"[Server] Error accepting connection: {e}")
            break

if __name__ == "__main__":
    try:
        accept_connections()
    except KeyboardInterrupt:
        print("[Server] Shutting down.")
        sys.exit(0)
