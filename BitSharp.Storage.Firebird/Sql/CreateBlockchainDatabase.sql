CREATE TABLE BlockchainMetadata
(
    Guid CHAR(16) CHARACTER SET OCTETS NOT NULL,
    RootBlockHash CHAR(32) CHARACTER SET OCTETS NOT NULL,
    TotalWork CHAR(64) CHARACTER SET OCTETS NOT NULL,
    IsComplete INTEGER NOT NULL,
	CONSTRAINT PK_BlockchainMetaData PRIMARY KEY
	(
        Guid
	)
);

CREATE TABLE ChainedBlocks
(
    Guid CHAR(16) CHARACTER SET OCTETS NOT NULL,
    RootBlockHash CHAR(32) CHARACTER SET OCTETS NOT NULL,
	BlockHash CHAR(32) CHARACTER SET OCTETS NOT NULL,
	PreviousBlockHash CHAR(32) CHARACTER SET OCTETS NOT NULL,
	Height INTEGER NOT NULL,
	TotalWork CHAR(64) CHARACTER SET OCTETS NOT NULL,
	CONSTRAINT PK_ChainedBlocks PRIMARY KEY
	(
        Guid,
        RootBlockHash,
		BlockHash
	)
);

CREATE INDEX IX_ChainedBlocks_Guid_RootHash ON ChainedBlocks ( Guid, RootBlockHash );

CREATE TABLE UtxoData
(
	Guid CHAR(16) CHARACTER SET OCTETS NOT NULL,
	RootBlockHash CHAR(32) CHARACTER SET OCTETS NOT NULL,
	UtxoChunkBytes BLOB SUB_TYPE BINARY NOT NULL
);

CREATE INDEX IX_UtxoData_Guid_RootBlockHash ON UtxoData ( Guid, RootBlockHash );
