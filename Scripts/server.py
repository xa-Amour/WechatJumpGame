#!/usr/bin/env python
#
"""Jump game server code, accomplished mainly by select multiplexing."""
"""Usage: python ./server.py """

import errno
import random
import select
import socket
import time


class Server(object):

    def __init__(self):
        self.conneclst = []
        self.outputs = []
        self.sock = None
        self.host = ''
        self.port = 50000
        self.size = 1024
        self.backlog = 10
        self.max_dist = 1.5
        self.min_dist = 0.8
        self.min_sca = 0.3
        self.max_sca = 0.5
        self.highest_score = []
        self.room_list = []
        self.room_info = []
        self.wait_time = 10
        self.max_player_count = 3

    def connect(self):
        self.host = socket.gethostbyname(socket.getfqdn(socket.gethostname()))
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.bind((self.host, self.port))
        self.sock.listen(self.backlog)
        self.sock.setblocking(0)

    def run(self):
        self.connect()
        self.conneclst.append(self.sock)
        print '[info]: server is running'
        while True:
            rlist, wlist, elist = select.select(
                self.conneclst, self.outputs, [], 0)
            for r in rlist:
                if r is self.sock:
                    client, addr = self.sock.accept()
                    client.setblocking(0)
                    print str(addr) + ' has connected'
                    self.conneclst.append(client)
                    self.outputs.append(client)
                    self.join_room(client)
                else:
                    total_data, break_flag = '', False
                    while True:
                        try:
                            data = r.recv(self.size)
                            if data:
                                total_data += data
                            disconnected = not data
                        except socket.error as e:
                            if e.args[0] == errno.EWOULDBLOCK:
                                break
                            disconnected = True
                        if disconnected:
                            self.handle_disconnection(r)
                            break_flag = True
                            break
                    if break_flag:
                        continue
                    else:
                        self.handle(total_data, r)
            self.count_down()

    def send_data(self, data, c):
        while True:
            try:
                c.sendall(data)
                break
            except socket.error as e:
                if e.args[0] == errno.EWOULDBLOCK:
                    print c + " : EWOULDBLOCK"
                    continue
                print "[info]: Socket Error"
                break

    def handle(self, data, r):
        data_list = data.split('\n')
        for data_list_item in data_list:
            data_item = data_list_item.split(' ')
            for room_index, room in enumerate(self.room_list):
                if r in room:
                    for c in room:
                        if c != r:
                            if data_item[0] == "0" and len(data_item) == 3:
                                print "receive: " + data_list_item
                                self.send_data(
                                    "4" + data_list_item[1:] + '\n', c)
                            elif data_item[0] == "1" and len(data_item) == 12:
                                self.send_data(
                                    "5" + data_list_item[1:] + '\n', c)
                            elif data_item[0] == "2" and len(data_item) == 2:
                                print "receive: " + data_list_item
                                self.send_data(
                                    "6" + data_list_item[1:] + '\n', c)

                    if data_item[0] == "0" and len(
                            data_item) == 3 and self.highest_score[room_index] < int(data_item[2]):
                        self.highest_score[room_index] = int(data_item[2])
                        send_data = self.gen_cube_param()
                        print "send: " + send_data
                        for c in room:
                            self.send_data(send_data, c)

                    if data_item[0] == "2" and len(data_item) == 2:
                        self.room_info[room_index][3] -= 1
                        self.send_data(
                            "7 " + str(self.room_info[room_index][3]) + '\n', r)
                        if self.room_info[room_index][3] <= 1:
                            for c in room:
                                self.send_data("8" + '\n', c)
                    break

    def count_down(self):
        for info_index, info in enumerate(self.room_info):
            if info[0] == "ready" and info[2] > 0 and time.time() - \
                    info[1] > 1:
                info[1] = time.time()
                info[2] -= 1
                send_data = "2 " + str(info[2]) + '\n'
                for c in self.room_list[info_index]:
                    self.send_data(send_data, c)
                if info[2] == 0:
                    info[0] = "in game"
                    send_data = self.gen_cube_param()
                    send_data = send_data[0:2] + '0' + send_data[3:]
                    for c in self.room_list[info_index]:
                        self.send_data(send_data, c)

    def join_room(self, client):
        in_room_flag, my_room = False, 0
        for room_index, room in enumerate(self.room_list):
            if len(room) < 2 and self.room_info[room_index][0] == "not ready":
                room.append(client)
                in_room_flag, my_room = True, room
                self.room_info[room_index] = [
                    "ready", time.time(), self.wait_time, 2]
                break
            elif len(room) < self.max_player_count and self.room_info[room_index][0] == "ready":
                room.append(client)
                in_room_flag, my_room = True, room
                self.room_info[room_index][3] += 1
                break
        if not in_room_flag:
            self.room_list.append([client])
            self.room_info.append(["not ready", 0.0, self.wait_time, 1])
            self.highest_score.append(0)
            my_room = [client]

        tmp_id = str(len(my_room))
        for c in my_room:
            if c != client:
                self.send_data("1 " + tmp_id + '\n', c)
                self.send_data("1 " + str(my_room.index(c) + 1) + '\n', client)
            else:
                self.send_data("0 " + tmp_id + '\n', c)

    def gen_cube_param(self):
        direction = str(self.random_direction())
        distance = str(self.random_distance())
        scale = str(self.random_scale())
        color = self.random_color()
        send_data = "3 " + direction + ' ' + distance + ' ' + scale + \
                    ' ' + str(color[0]) + ' ' + str(color[1]) + ' ' + str(color[2])
        return send_data

    def random_direction(self):
        return random.randint(0, 1)

    def random_distance(self):
        return round(random.uniform(self.min_dist, self.max_dist), 3)

    def random_scale(self):
        return round(random.uniform(self.min_sca, self.max_sca), 3)

    def random_color(self):
        return [
            round(
                random.random(), 3), round(
                random.random(), 3), round(
                random.random(), 3)]

    def handle_disconnection(self, r):
        print str(r.getpeername()) + ' is disconnected'
        self.conneclst.remove(r)
        self.outputs.remove(r)
        for room_index, room in enumerate(self.room_list):
            if r in room:
                tmp_id = str(room.index(r) + 1)
                room.remove(r)
                if len(room) == 0:
                    del self.room_list[room_index]
                    del self.room_info[room_index]
                    del self.highest_score[room_index]
                else:
                    if self.room_info[room_index][0] == "ready":
                        self.room_info[room_index][3] -= 1
                        for c in room:
                            self.send_data("9 " + tmp_id + '\n', c)
                    elif self.room_info[room_index][0] == "in game":
                        for c in room:
                            self.send_data("6 " + tmp_id + '\n', c)
                        self.room_info[room_index][3] -= 1
                        if self.room_info[room_index][3] <= 1:
                            for c in room:
                                self.send_data("8" + '\n', c)


if __name__ == '__main__':
    Server().run()
